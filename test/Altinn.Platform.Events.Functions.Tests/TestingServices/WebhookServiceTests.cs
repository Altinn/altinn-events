using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services;

using CloudNative.CloudEvents;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices
{
    public class WebhookServiceTests
    {
        private const string cloudEventId = "1337";

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

    }
}
