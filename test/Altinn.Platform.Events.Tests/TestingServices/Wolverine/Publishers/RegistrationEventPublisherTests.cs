using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Wolverine.Commands;
using Altinn.Platform.Events.Wolverine.Publishers;

using CloudNative.CloudEvents;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Publishers;

/// <summary>
/// A collection of tests related to <see cref="RegistrationEventPublisher"/>.
/// </summary>
public class RegistrationEventPublisherTests
{
    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called with a cloud event.
    /// Expected result:
    ///   The event is sent to the message bus as a RegisterEventCommand.
    /// Success criteria:
    ///   IMessageBus.SendAsync is called once with a RegisterEventCommand.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_ValidCloudEvent_SendsToMessageBus()
    {
        // Arrange
        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<RegisterEventCommand>())).Returns(ValueTask.CompletedTask);

        var publisher = new RegistrationEventPublisher(busMock.Object);

        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "instance.created",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/apps-test/"),
            Time = DateTime.Now,
            Subject = "/party/456456"
        };

        // Act
        await publisher.PublishRegistrationEvent(cloudEvent);

        // Assert
        busMock.Verify(b => b.SendAsync(It.IsAny<RegisterEventCommand>()), Times.Once);
    }
}
