using Altinn.Platform.Events.Functions.Clients.Interfaces;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingFunctions
{
    public class EventRegistrationTests
    {
        private readonly string serializedCloudEvent; 

        public EventRegistrationTests()
        {
            serializedCloudEvent = "{" +
                "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
                "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
                "\"specversion\":\"1.0\"," +
                "\"type\":\"app.instance.created\"," +
                "\"extenstionatt1\":\"Stephanie er kul\"," +
                "\"extenstionatt2\":\"2.718281828\"" +
                "}";
        }

        [Fact]
        public async Task Run_SaveEventFails_PostInboundNotCalled()
        {
            // Arrange
            Mock<IEventsClient> clientMock = new();

            clientMock.Setup(c => c.SaveCloudEvent(It.IsAny<CloudEvent>()))
               .ThrowsAsync(new HttpRequestException("// SaveCloudEvent with id {cloudEvent.Id} failed with status code {statusCode}"));

            clientMock.
                Setup(c => c.PostInbound(It.IsAny<CloudEvent>()))
                .Returns(Task.CompletedTask);

            EventsRegistration sut = new EventsRegistration(clientMock.Object);

            // Act
            try
            {
                await sut.Run(serializedCloudEvent);
            }
            catch (HttpRequestException)
            {
            }

            // Assert
            clientMock.Verify(c => c.SaveCloudEvent(It.IsAny<CloudEvent>()), Times.Once);
            clientMock.Verify(c => c.PostInbound(It.IsAny<CloudEvent>()), Times.Never);
        }

        [Fact]
        public async Task Run_SaveEventSucceeds_PostInboundCalled()
        {
            // Arrange
            Mock<IEventsClient> clientMock = new();

            clientMock.Setup(c => c.SaveCloudEvent(It.IsAny<CloudEvent>()))
                .Returns(Task.CompletedTask);

            clientMock.
                Setup(c => c.PostInbound(It.IsAny<CloudEvent>()))
                .Returns(Task.CompletedTask);

            EventsRegistration sut = new EventsRegistration(clientMock.Object);

            // Act
            await sut.Run(serializedCloudEvent);

            // Assert
            clientMock.Verify(c => c.SaveCloudEvent(It.IsAny<CloudEvent>()), Times.Once);
            clientMock.Verify(c => c.PostInbound(It.IsAny<CloudEvent>()), Times.Once);
        }
    }
}
