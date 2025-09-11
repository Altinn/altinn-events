using Altinn.Platform.Events.Functions.Clients.Interfaces;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingFunctions;

public class EventsRegistrationTests
{
    private readonly string serializedCloudEvent;

    public EventsRegistrationTests()
    {
        serializedCloudEvent = "{" +
            "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
            "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
            "\"resource\":\"urn:altinn:resource:app_ttd_apps-test\"," +
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

        EventsRegistration sut = new(clientMock.Object, NullLogger<EventsRegistration>.Instance);

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

        EventsRegistration sut = new EventsRegistration(clientMock.Object, NullLogger<EventsRegistration>.Instance);

        // Act
        await sut.Run(serializedCloudEvent);

        // Assert
        clientMock.Verify(c => c.SaveCloudEvent(It.IsAny<CloudEvent>()), Times.Once);
        clientMock.Verify(c => c.PostInbound(It.IsAny<CloudEvent>()), Times.Once);
    }

    [Theory]
    [InlineData("urn:altinn:resource:altinnapp.ttd.apps-test", "https://ttd.apps.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319", "urn:altinn:resource:app_ttd_apps-test")]
    [InlineData("urn:altinn:resource:altinnapp.skd.endring-av-navn", "https://ttd.apps.altinn.cloud/skd/endring-av-navn/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319", "urn:altinn:resource:app_skd_endring-av-navn")]
    [InlineData("urn:altinn:resource:app_ttd_apps-test", "https://ttd.apps.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319", "urn:altinn:resource:app_ttd_apps-test")]
    [InlineData("urn:altinn:resource:nav-annen-ting", "https://external-url.com", "urn:altinn:resource:nav-annen-ting")]

    public void EnsureCorrectResourceFormat_ConfirmMapping(string inputResource, string source, string outputResource)
    {
        // Arrange
        CloudEvent cloudEvent = new CloudEvent()
        {
            Source = new Uri(source),
            Type = "testevent"
        };

        cloudEvent["resource"] = inputResource;

        // Act
        EventsRegistration.EnsureCorrectResourceFormat(cloudEvent);

        // Assert
        Assert.Equal(outputResource, cloudEvent["resource"]);
    }
}
