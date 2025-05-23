﻿using System.Net;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices
{
    public class WebhookServiceTests
    {
        private const string _cloudEventId = "1337";
        private Mock<IEventsClient> _eventsClientMock = new Mock<IEventsClient>();

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
            Mock<ILogger<WebhookService>> loggerMock = new Mock<ILogger<WebhookService>>();

            HttpClient actualClient = new();

            // Act
            _ = new WebhookService(actualClient, null, _eventsOutboundSettings, loggerMock.Object);

            // Assert
            Assert.Equal(300, actualClient.Timeout.TotalSeconds);
        }

        [Fact]
        public void GetPayload_CloudEventExtentionAttributesPersisted()
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
            Mock<ILogger<WebhookService>> loggerMock = new Mock<ILogger<WebhookService>>();
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.ServiceUnavailable });

            var sut = new WebhookService(new HttpClient(handlerMock.Object), _eventsClientMock.Object, _eventsOutboundSettings, loggerMock.Object);

            var cloudEventEnvelope = new CloudEventEnvelope()
            {
                Endpoint = new Uri("https://vg.no"),
                CloudEvent = _minimalCloudEvent
            };

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.Send(cloudEventEnvelope));

            // Assert
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Exactly(2));
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task Send_ClientReturnsSuccessCode_NoLoggingOrException()
        {
            // Arrange
            Mock<ILogger<WebhookService>> loggerMock = new Mock<ILogger<WebhookService>>();
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var sut = new WebhookService(new HttpClient(handlerMock.Object), _eventsClientMock.Object, _eventsOutboundSettings, loggerMock.Object);

            var cloudEventEnvelope = new CloudEventEnvelope()
            {
                Endpoint = new Uri("https://vg.no"),
                CloudEvent = _minimalCloudEvent
            };

            // Act
            await sut.Send(cloudEventEnvelope);

            // Assert
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Never);
            handlerMock.VerifyAll();
        }

        private static Mock<HttpMessageHandler> CreateMessageHandlerMock(string clientEndpoint, HttpResponseMessage response)
        {
            var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            messageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.Equals(clientEndpoint)), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    return response;
                })
                .Verifiable();

            return messageHandlerMock;
        }
    }
}
