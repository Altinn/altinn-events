using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using System.Net;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices
{
    public class WebhookServiceTests
    {
        private const string cloudEventId = "1337";
        private CloudEvent _minimalCloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = cloudEventId,
            Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
            Type = "automated.test"
        };


        [Fact]
        public void GetPayload_CloudEventExtentionAttributesPersisted()
        {
            // Arrange
            string expectedPayload =
               "{" +
               "\"specversion\":\"1.0\"," +
               $"\"id\":\"{cloudEventId}\"," +
               "\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\"," +
               "\"type\":\"automated.test\"," +
               "\"atta\":\"If the wolf eats your grandma\"," +
               "\"attb\":\"Give the wolf a banana\"" +
               "}";

            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = cloudEventId,
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

            var sut = new WebhookService(null, null);

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
                   "\"specversion\":\"1.0\"," +
                   $"\"id\":\"{cloudEventId}\"," +
                   "\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\"," +
                   "\"type\":\"automated.test\"" +
                   "}\"" +
                "}";

            CloudEventEnvelope input = new()
            {
                CloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Id = cloudEventId,
                    Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                    Type = "automated.test"
                },
                SubscriptionId = 1337,
                Consumer = "/party/test",
                Endpoint = new Uri("https://hooks.slack.com/services/org/channel"),
                Pushed = DateTime.UtcNow
            };

            var sut = new WebhookService(null, null);

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
               $"\"id\":\"{cloudEventId}\"," +
               "\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test\"," +
               "\"type\":\"automated.test\"" +
               "}";

            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = cloudEventId,
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

            var sut = new WebhookService(null, null);

            // Act
            var actual = sut.GetPayload(input);

            // Assert
            Assert.Equal(expectedPayload, actual);
        }

        [Fact]
        public async Task Send_ClientReturnsNonSuccessCode_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            Mock<ILogger<IWebhookService>> loggerMock = new Mock<ILogger<IWebhookService>>();
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.ServiceUnavailable });

            var sut = new WebhookService(new HttpClient(handlerMock.Object), loggerMock.Object);

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
            Mock<ILogger<IWebhookService>> loggerMock = new Mock<ILogger<IWebhookService>>();
            var handlerMock = CreateMessageHandlerMock("https://vg.no", new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var sut = new WebhookService(new HttpClient(handlerMock.Object), loggerMock.Object);

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

            return messageHandlerMock; ;
        }
    }
}
