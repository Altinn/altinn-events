using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

public class WebhookServiceTest
{
    private const string _cloudEventId = "1337";
    private readonly Mock<ITraceLogService> _traceLogServiceMock = new();

    private readonly CloudEvent _minimalCloudEvent = new(CloudEventsSpecVersion.V1_0)
    {
        Id = _cloudEventId,
        Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
        Type = "automated.test"
    };

    private readonly IOptions<EventsOutboundSettings> _eventsOutboundSettings =
        Options.Create(new EventsOutboundSettings());

    [Fact]
    public void Ctor_HttpClientHasRequestTimeout()
    {
        // Arrange
        Mock<ILogger<WebhookService>> loggerMock = new();

        HttpClient actualClient = new();

        // Act
        _ = new WebhookService(actualClient, null, _eventsOutboundSettings, loggerMock.Object);

        // Assert
        Assert.Equal(300, actualClient.Timeout.TotalSeconds);
    }

    [Fact]
    public void GetPayload_CloudEventExtensionAttributesPersisted()
    {
        // Arrange
        string expectedPayload =
           "{" +
           "\"specversion\":\"1.0\"," +
           $"\"id\":\"{_cloudEventId}\"," +
           "\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\"," +
           "\"type\":\"automated.test\"," +
           "\"atta\":\"If the wolf eats your grandma\"," +
           "\"attb\":\"Give the wolf a banana\"" +
           "}";

        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = _cloudEventId,
            Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
            Type = "automated.test"
        };

        cloudEvent.SetAttributeFromString("atta", "If the wolf eats your grandma");
        cloudEvent.SetAttributeFromString("attb", "Give the wolf a banana");

        CloudEventEnvelope input = new()
        {
            CloudEvent = cloudEvent,
            SubscriptionId = 1337,
            Consumer = "/party/test",
            Endpoint = new Uri("https://skd.mottakssystem.no/events"),
            Pushed = DateTime.UtcNow
        };

        var sut = new WebhookService(new HttpClient(), null, _eventsOutboundSettings, null);

        // Act
        var actual = sut.GetPayload(input);

        // Assert
        Assert.Equal(expectedPayload, actual);
    }

    [Fact]
    public void GetPayload_SlackUrlProvided_FullSlackEnvelopeSerialized()
    {
        // Arrange
        string expectedPayload =
           "{" +
           "\"text\": " +
               "\"{" +
               "\\\"specversion\\\":\\\"1.0\\\"," +
               $"\\\"id\\\":\\\"{_cloudEventId}\\\"," +
               "\\\"source\\\":\\\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\\\"," +
               "\\\"type\\\":\\\"automated.test\\\"" +
               "}\"" +
            "}";

        CloudEventEnvelope input = new()
        {
            CloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = _cloudEventId,
                Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                Type = "automated.test"
            },
            SubscriptionId = 1337,
            Consumer = "/party/test",
            Endpoint = new Uri("https://hooks.slack.com/services/org/channel"),
            Pushed = DateTime.UtcNow
        };

        var sut = new WebhookService(new HttpClient(), null, _eventsOutboundSettings, null);

        // Act
        var actual = sut.GetPayload(input);

        // Assert
        Assert.Equal(expectedPayload, actual);
    }

    [Fact]
    public void GetPayload_GeneralUrlProvided_OnlyCloudEventSerialized()
    {
        // Arrange
        string expectedPayload =
           "{" +
           "\"specversion\":\"1.0\"," +
           $"\"id\":\"{_cloudEventId}\"," +
           "\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\"," +
           "\"type\":\"automated.test\"" +
           "}";

        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = _cloudEventId,
            Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
            Type = "automated.test"
        };

        CloudEventEnvelope input = new()
        {
            CloudEvent = cloudEvent,
            SubscriptionId = 1337,
            Consumer = "/party/test",
            Endpoint = new Uri("https://skd.mottakssystem.no/events"),
            Pushed = DateTime.UtcNow
        };

        var sut = new WebhookService(new HttpClient(), null, _eventsOutboundSettings, null);

        // Act
        var actual = sut.GetPayload(input);

        // Assert
        Assert.Equal(expectedPayload, actual);
    }

    [Fact]
    public async Task Send_ClientReturnsNonSuccessCode_ErrorLoggedAndExceptionThrown()
    {
        // Arrange
        Mock<ILogger<WebhookService>> loggerMock = new();
        var handlerMock = CreateMessageHandlerMock(
            "https://vg.no",
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service unavailable")
            });

        var sut = new WebhookService(new HttpClient(handlerMock.Object), _traceLogServiceMock.Object, _eventsOutboundSettings, loggerMock.Object);

        var cloudEventEnvelope = new CloudEventEnvelope()
        {
            Endpoint = new Uri("https://vg.no"),
            CloudEvent = _minimalCloudEvent
        };

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.Send(cloudEventEnvelope, CancellationToken.None));

        // Assert
        loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Exactly(2));
        handlerMock.VerifyAll();
    }

    [Fact]
    public async Task Send_ClientReturnsSuccessCode_NoLoggingOrException()
    {
        // Arrange
        Mock<ILogger<WebhookService>> loggerMock = new();
        var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var sut = new WebhookService(new HttpClient(handlerMock.Object), _traceLogServiceMock.Object, _eventsOutboundSettings, loggerMock.Object);

        var cloudEventEnvelope = new CloudEventEnvelope()
        {
            Endpoint = new Uri("https://vg.no"),
            CloudEvent = _minimalCloudEvent
        };

        // Act
        await sut.Send(cloudEventEnvelope, CancellationToken.None);

        // Assert
        loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Never);
        handlerMock.VerifyAll();
    }

    [Fact]
    public void GetPayload_NullCloudEvent_ReturnsEmptyString()
    {
        // Arrange
        CloudEventEnvelope input = new()
        {
            CloudEvent = null,
            SubscriptionId = 1337,
            Consumer = "/party/test",
            Endpoint = new Uri("https://skd.mottakssystem.no/events"),
        };

        var sut = new WebhookService(new HttpClient(), null, _eventsOutboundSettings, null);

        // Act
        var actual = sut.GetPayload(input);

        // Assert
        Assert.Equal(string.Empty, actual);
    }

    [Fact]
    public void GetPayload_NullEndpoint_ReturnsSerializedCloudEvent()
    {
        // Arrange
        CloudEventEnvelope input = new()
        {
            CloudEvent = _minimalCloudEvent,
            SubscriptionId = 1337,
            Consumer = "/party/test",
            Endpoint = null,
        };

        var sut = new WebhookService(new HttpClient(), null, _eventsOutboundSettings, null);

        // Act
        var actual = sut.GetPayload(input);

        // Assert
        Assert.Contains(_cloudEventId, actual);
    }

    [Fact]
    public async Task Send_ClientThrowsException_LogsAndRethrows()
    {
        // Arrange
        Mock<ILogger<WebhookService>> loggerMock = new();
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var sut = new WebhookService(new HttpClient(handlerMock.Object), _traceLogServiceMock.Object, _eventsOutboundSettings, loggerMock.Object);

        var cloudEventEnvelope = new CloudEventEnvelope()
        {
            Endpoint = new Uri("https://unreachable.example.com"),
            CloudEvent = _minimalCloudEvent
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.Send(cloudEventEnvelope, CancellationToken.None));

        loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
    }

    private static Mock<HttpMessageHandler> CreateMessageHandlerMock(string clientEndpoint, HttpResponseMessage response)
    {
        var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        messageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.AbsoluteUri.TrimEnd('/') == clientEndpoint.TrimEnd('/')), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                return response;
            })
            .Verifiable();

        return messageHandlerMock;
    }
}
