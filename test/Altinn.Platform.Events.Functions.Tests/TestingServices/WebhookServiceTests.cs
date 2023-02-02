using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices
{
    public class WebhookServiceTests
    {

        [Fact]
        public void GetPayload_CloudEventExtentionAttributesPersisted()
        {
            // Arrange
            string expectedPayload = "";

            CloudEventEnvelope input = new()
            {
                CloudEvent = new(CloudNative.CloudEvents.CloudEventsSpecVersion.V1_0)
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                    Type = "automated.test"
                },
                SubscriptionId = 1337,
                Consumer = "/party/test",
                Endpoint = new Uri("https://skd.mottakssystem.no/events"),
                Pushed = DateTime.UtcNow
            };

            var sut = new WebhookService(null, null);
            var actual = sut.GetPayload(input);

            Assert.Equal(expectedPayload, actual);
        }

        [Fact]
        public void GetPayload_SlackUrlProvided_FullSlackEnvelopeSerialized()
        {

        }

        [Fact]
        public void GetPayload_GeneralUrlProvided_OnlyCloudEventSerialized()
        {

        }

    }
}
