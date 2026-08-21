using System.Net;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Wolverine.Models;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="AsbWebhookService"/>.
    /// </summary>
    public class AsbWebhookServiceTests
    {
        private const string _cloudEventId = "1337";
        private readonly Mock<IEventsClient> _eventsClientMock = new();

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
            Mock<ILogger<AsbWebhookService>> loggerMock = new();
            HttpClient actualClient = new();

            // Act
            _ = new AsbWebhookService(actualClient, _eventsClientMock.Object, _eventsOutboundSettings, loggerMock.Object);

            // Assert
            Assert.Equal(300, actualClient.Timeout.TotalSeconds);
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
                CloudEvent = _minimalCloudEvent,
                SubscriptionId = 1337,
                Consumer = "/party/test",
                Endpoint = new Uri("https://hooks.slack.com/services/org/channel"),
                Pushed = DateTime.UtcNow
            };

            var sut = new AsbWebhookService(new HttpClient(), _eventsClientMock.Object, _eventsOutboundSettings, null);

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

            CloudEventEnvelope input = new()
            {
                CloudEvent = _minimalCloudEvent,
                SubscriptionId = 1337,
                Consumer = "/party/test",
                Endpoint = new Uri("https://skd.mottakssystem.no/events"),
                Pushed = DateTime.UtcNow
            };

            var sut = new AsbWebhookService(new HttpClient(), _eventsClientMock.Object, _eventsOutboundSettings, null);

            // Act
            var actual = sut.GetPayload(input);

            // Assert
            Assert.Equal(expectedPayload, actual);
        }

        [Fact]
        public void GetPayload_CloudEventMissing_ReturnsEmptyString()
        {
            // Arrange
            CloudEventEnvelope input = new()
            {
                SubscriptionId = 1337,
                Consumer = "/party/test",
                Endpoint = new Uri("https://skd.mottakssystem.no/events"),
                Pushed = DateTime.UtcNow
            };

            var sut = new AsbWebhookService(new HttpClient(), _eventsClientMock.Object, _eventsOutboundSettings, null);

            // Act
            var actual = sut.GetPayload(input);

            // Assert
            Assert.Equal(string.Empty, actual);
        }

        [Fact]
        public async Task Send_ClientReturnsNonSuccessCode_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            Mock<ILogger<AsbWebhookService>> loggerMock = new();
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.ServiceUnavailable });

            var sut = new AsbWebhookService(new HttpClient(handlerMock.Object), _eventsClientMock.Object, _eventsOutboundSettings, loggerMock.Object);

            var cloudEventEnvelope = new CloudEventEnvelope
            {
                Endpoint = new Uri("https://vg.no"),
                CloudEvent = _minimalCloudEvent
            };

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(() => sut.Send(cloudEventEnvelope, CancellationToken.None));

            // Assert
            _eventsClientMock.Verify(
                x => x.LogWebhookHttpStatusCode(
                    It.Is<Altinn.Platform.Events.Functions.Models.CloudEventEnvelope>(e => e.SubscriptionId == cloudEventEnvelope.SubscriptionId && e.Endpoint == cloudEventEnvelope.Endpoint),
                    HttpStatusCode.ServiceUnavailable,
                    false),
                Times.Once);
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task Send_ClientReturnsSuccessCode_NoLoggingOrException()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var sut = new AsbWebhookService(new HttpClient(handlerMock.Object), _eventsClientMock.Object, _eventsOutboundSettings, null);

            var cloudEventEnvelope = new CloudEventEnvelope
            {
                Endpoint = new Uri("https://vg.no"),
                CloudEvent = _minimalCloudEvent
            };

            // Act
            await sut.Send(cloudEventEnvelope, CancellationToken.None);

            // Assert
            _eventsClientMock.Verify(
                x => x.LogWebhookHttpStatusCode(
                    It.Is<Altinn.Platform.Events.Functions.Models.CloudEventEnvelope>(e => e.SubscriptionId == cloudEventEnvelope.SubscriptionId),
                    HttpStatusCode.OK,
                    true),
                Times.Once);
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task Send_ClientThrows_LogsAndRethrowsAndSkipsEventClientLogging()
        {
            // Arrange
            Mock<ILogger<AsbWebhookService>> loggerMock = new();
            var handlerMock = CreateThrowingMessageHandlerMock("https://vg.no", new HttpRequestException("boom"));

            var sut = new AsbWebhookService(new HttpClient(handlerMock.Object), _eventsClientMock.Object, _eventsOutboundSettings, loggerMock.Object);

            var cloudEventEnvelope = new CloudEventEnvelope
            {
                Endpoint = new Uri("https://vg.no"),
                CloudEvent = _minimalCloudEvent,
                SubscriptionId = 1337
            };

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(() => sut.Send(cloudEventEnvelope, CancellationToken.None));

            // Assert
            _eventsClientMock.Verify(
                x => x.LogWebhookHttpStatusCode(It.IsAny<Altinn.Platform.Events.Functions.Models.CloudEventEnvelope>(), It.IsAny<HttpStatusCode>(), It.IsAny<bool>()),
                Times.Never);
            handlerMock.VerifyAll();
        }

        private static Mock<HttpMessageHandler> CreateMessageHandlerMock(string clientEndpoint, HttpResponseMessage response)
        {
            var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            messageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.Equals(clientEndpoint)), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => response)
                .Verifiable();

            return messageHandlerMock;
        }

        private static Mock<HttpMessageHandler> CreateThrowingMessageHandlerMock(string clientEndpoint, Exception exception)
        {
            var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            messageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.Equals(clientEndpoint)), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(exception)
                .Verifiable();

            return messageHandlerMock;
        }
    }
}
