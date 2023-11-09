using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingFunctions
{
    public class SubscriptionValidationTests
    {
        private static Subscription _subscription;
        private static string _serializedSubscription;

        public SubscriptionValidationTests()
        {
            _subscription = new()
            {
                Consumer = "/org/ttd",
                EndPoint = new Uri("https://hooks.slack.com/services/org/channel/"),
                Id = 1337
            };

            _serializedSubscription = "{\"EndPoint\":\"https://hooks.slack.com/services/org/channel\",\"Id\":1337,\"SourceFilter\":null,\"SubjectFilter\":null,\"AlternativeSubjectFilter\":null,\"TypeFilter\":null,\"Consumer\":\"/org/ttd\",\"CreatedBy\":null,\"Created\":\"0001-01-01T00:00:00\"}";
        }

        [Fact]
        public void CreateValidateEvent_SubscriptionMappedCorrectlyIntoCloudEventEnvelope()
        {
            // Arrange
            string expectedConsumer = "/org/ttd";
            Uri expectedEndpoint = new Uri("https://hooks.slack.com/services/org/channel/");
            string expectedCloudEventType = "platform.events.validatesubscription";
            CloudEventsSpecVersion expectedSpecVersion = CloudEventsSpecVersion.V1_0;

            var sut = new SubscriptionValidation(
                Options.Create(new PlatformSettings
                {
                    ApiEventsEndpoint = "https://platform.at22.altinn.cloud/events/api/v1/"
                }),
                null,
                null);

            // Act
            var actual = sut.CreateValidateEvent(_subscription);

            // Assert
            Assert.Equal(expectedConsumer, actual.Consumer);
            Assert.Equal(expectedEndpoint, actual.Endpoint);
            Assert.Equal(expectedCloudEventType, actual.CloudEvent.Type);
            Assert.Equal(expectedSpecVersion, actual.CloudEvent.SpecVersion);
        }

        [Fact]
        public async Task Run_SendWebhookSucceeds_ValidateSubscriptionCalled()
        {
            // Arrange
            Mock<IWebhookService> webhookServiceMock = new();
            webhookServiceMock.Setup(wh => wh.Send(It.IsAny<CloudEventEnvelope>()))
                .Returns(Task.CompletedTask);

            Mock<IEventsClient> clientMock = new();

            clientMock.Setup(c => c.ValidateSubscription(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var sut = new SubscriptionValidation(
                Options.Create(new PlatformSettings { ApiEventsEndpoint = "https://platform.at22.altinn.cloud/events/api/v1/" }),
                webhookServiceMock.Object,
                clientMock.Object);

            // Act
            await sut.Run(_serializedSubscription, null);

            // Assert
            webhookServiceMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task Run_SendWebhookFails_ValidateSubscriptionNotCalled()
        {
            // Arrange
            Mock<IWebhookService> webhookServiceMock = new();
            webhookServiceMock.Setup(wh => wh.Send(It.IsAny<CloudEventEnvelope>()))
               .ThrowsAsync(new HttpRequestException("// WebhookService // Send // Failed to send cloud event id {envelope.CloudEvent.Id}, subscriptionId: {envelope.SubscriptionId}. \\nReason: {reason} \\nResponse: {response}"));

            Mock<IEventsClient> clientMock = new();

            clientMock.Setup(c => c.ValidateSubscription(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var sut = new SubscriptionValidation(
                Options.Create(new PlatformSettings { ApiEventsEndpoint = "https://platform.at22.altinn.cloud/events/api/v1/" }),
                webhookServiceMock.Object,
                clientMock.Object);

            // Act
            try
            {
                await sut.Run(_serializedSubscription, null);
            }
            catch (HttpRequestException)
            {
            }

            // Assert
            webhookServiceMock.VerifyAll();
            clientMock.Verify(c => c.ValidateSubscription(It.IsAny<int>()), Times.Never);
        }
    }
}
