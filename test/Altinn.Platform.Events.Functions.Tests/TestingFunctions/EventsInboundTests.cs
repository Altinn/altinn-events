using Altinn.Platform.Events.Functions.Clients.Interfaces;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingFunctions
{
    public class EventsInboundTests
    {
        [Theory]
        [InlineData("{\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\",\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\",\"specversion\":\"1.0\",\"type\":\"app.instance.created\",\"subject\":\"/party/50012356\",\"time\":\"2023-01-13T09:47:41.1680188Z\",\"alternativesubject\":\"/person/16035001577\"}", 1, "alternativesubject", "/person/16035001577")]
        public async Task PostInbound_ConfirmDeserializationOfEvent(string serializedCloudEvent, int expectedExtensionAttributeCount, string extensionAttributeName, string expectedValue)
        {

            // Arrange
            CloudEvent serviceInput = null;
            Mock<IEventsClient> clientMock = new();
            clientMock.Setup(c => c.PostOutbound(It.IsAny<CloudEvent>()))
                .Callback<CloudEvent>(e => serviceInput = e)
                .Returns(Task.CompletedTask);

            EventsInbound sut = new EventsInbound(clientMock.Object);

            // Act
            await sut.Run(serializedCloudEvent, null);

            // Assert

            clientMock.VerifyAll();
            Assert.NotNull(serviceInput);
            AssertExpectedCloudEvent(serviceInput, expectedExtensionAttributeCount, extensionAttributeName, expectedValue);
        }

        [Fact]
        public async Task PostInbound_ConfirmDeserializationOfEvent_AlternativeSubject()
        {

            // Arrange
            string serializedCloudEvent = "{" +
                "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
                "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
                "\"specversion\":\"1.0\"," +
                "\"type\":\"app.instance.created\"," +
                "\"subject\":\"/party/50012356\"," +
                "\"time\":\"2023-01-13T09:47:41.1680188Z\"," +
                "\"alternativesubject\":\"/person/16035001577\"" +
                "}";

            Mock<IEventsClient> clientMock = new();
            clientMock.Setup(c => c.PostOutbound(It.Is<CloudEvent>(c => AssertExpectedCloudEvent(c, 1, "alternativesubject", "/person/16035001577"))))
                .Returns(Task.CompletedTask);

            EventsInbound sut = new EventsInbound(clientMock.Object);

            // Act
            await sut.Run(serializedCloudEvent, null);

            // Assert

            clientMock.VerifyAll();
        }

        [Fact]
        public async Task PostInbound_ConfirmDeserializationOfEvent_TwoExtensionAttributes()
        {

            // Arrange
            string serializedCloudEvent = "{" +
                "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
                "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
                "\"specversion\":\"1.0\"," +
                "\"type\":\"app.instance.created\"," +
                "\"extenstionatt1\":\"Stephanie er kul\"," +
                "\"extenstionatt2\":\"2.718281828\"" +
                "}";


            Mock<IEventsClient> clientMock = new();
            clientMock.Setup(c => c.PostOutbound(It.Is<CloudEvent>(c => AssertExpectedCloudEvent(c, 2, "extenstionatt1", "Stephanie er kul"))))
                .Returns(Task.CompletedTask);

            EventsInbound sut = new EventsInbound(clientMock.Object);

            // Act
            await sut.Run(serializedCloudEvent, null);

            // Assert

            clientMock.VerifyAll();
        }

        [Fact]
        public async Task PostInbound_ConfirmDeserializationOfEvent_DataPropertiesPerserved()
        {

            // Arrange
            CloudEvent serviceInput = null;

            string serializedCloudEvent = "{" +
                "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
                "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
                "\"specversion\":\"1.0\"," +
                "\"type\":\"app.instance.created\"," +
                "\"type\":\"app.instance.created\"," +
                "\"datacontenttype\": \"text/xml\"," +
                " \"data\": \" <note> <to>Tove</to> <from>Jani</from> <heading>Reminder</heading> <body>Don't forget me this weekend!</body> </note>\"," +
                "\"dataschema\":\"https://github.com/cloudevents\"" +
                "}";


            Mock<IEventsClient> clientMock = new();
            clientMock.Setup(c => c.PostOutbound(It.IsAny<CloudEvent>()))
                .Callback<CloudEvent>(e => serviceInput = e)
                .Returns(Task.CompletedTask);

            EventsInbound sut = new EventsInbound(clientMock.Object);

            // Act
            await sut.Run(serializedCloudEvent, null);

            // Assert

            clientMock.VerifyAll();
            Assert.NotNull(serviceInput);
            Assert.Contains("<heading>Reminder</heading>", serviceInput.Data.ToString());
            Assert.Equal("https://github.com/cloudevents", serviceInput.DataSchema.ToString());
            Assert.Equal("text/xml", serviceInput.DataContentType.ToString());
        }

        private bool AssertExpectedCloudEvent(CloudEvent cloudEvent, int expectedExtensionAttributeCount, string extensionAttributeName, string expectedValue)
        {
            string actualExtensionAttribute = cloudEvent
                .GetPopulatedAttributes()
                .Where(kv => kv.Key.ToString() == extensionAttributeName)
                .Select(kv => kv.Value)
                .FirstOrDefault()
                .ToString();

            Assert.Equal(expectedValue, actualExtensionAttribute);
            Assert.Equal(expectedExtensionAttributeCount, cloudEvent.ExtensionAttributes.ToList().Count);
            Assert.NotNull(cloudEvent.GetAttribute(extensionAttributeName));
            Assert.NotNull(cloudEvent.Source);
            Assert.NotNull(cloudEvent.Id);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);

            return true;
        }
    }
}