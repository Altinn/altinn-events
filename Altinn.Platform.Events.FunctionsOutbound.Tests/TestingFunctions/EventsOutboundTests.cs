using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingFunctions
{
    public class EventsOutboundTests
    {
        [Fact]
        public async Task Run_ConfirmDeserializationOCloudEventEnvelope_CloudEventPersisted()
        {
            // Arrange
            CloudEventEnvelope actualServiceInput = null;

            string serializedCloudEnvelope = "{" +
                "\"Pushed\": \"2023-01-17T16:09:10.9090958+00:00\"," +
                "  \"Endpoint\": \"https://hooks.slack.com/services/weebhook-endpoint\"," +
                "  \"Consumer\": \"/org/ttd\"," +
                "  \"SubscriptionId\": 427," +
                "  \"CloudEvent\": {" +
                    " \"specversion\": \"1.0\"," +
                    " \"id\": \"42849881-3659-4ff4-9ee1-c577646ea44b\"," +
                    " \"source\": \"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d\"," +
                    " \"type\": \"app.instance.created\"," +
                    " \"subject\": \"/party/50002108\"," +
                    " \"time\": \"2023-01-17T16:09:07.3146561Z\"," +
                    " \"alternativesubject\": \"/person/01014922047\"}}";

            Mock<IWebhookService> webhookServiceMock = new();
            webhookServiceMock.Setup(wh => wh.Send(It.Is<CloudEventEnvelope>(cee => cee.CloudEvent != null)))
                .Callback<CloudEventEnvelope>(cee => actualServiceInput = cee)
                .Returns(Task.CompletedTask);

            var sut = new IsolatedFunctions.EventsOutbound(webhookServiceMock.Object);

            // Act
            await sut.Run(serializedCloudEnvelope);

            // Assert
            webhookServiceMock.VerifyAll();
            Assert.Equal(CloudEventsSpecVersion.V1_0, actualServiceInput.CloudEvent.SpecVersion);
            Assert.Equal("https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d", actualServiceInput.CloudEvent.Source.ToString());
            Assert.Equal(427, actualServiceInput.SubscriptionId);
        }
    }
}
