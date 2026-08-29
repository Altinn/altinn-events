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
        await publisher.PublishRegistrationEvent(cloudEvent, null);

        // Assert
        busMock.Verify(b => b.SendAsync(It.IsAny<RegisterEventCommand>()), Times.Once);
    }

    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called with a cloud event and an idempotency id.
    /// Expected result:
    ///   The RegisterEventCommand sent to the message bus carries the same idempotency id and the serialized payload.
    /// Success criteria:
    ///   IMessageBus.SendAsync is called once with a RegisterEventCommand whose IdempotencyId matches and whose
    ///   Payload deserializes back to the original cloud event id.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_WithIdempotencyId_CommandCarriesIdAndPayload()
    {
        // Arrange
        const string idempotencyId = "d1525c79-cda8-4fef-b95c-feb3e7be89ec";
        RegisterEventCommand capturedCommand = null;

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<RegisterEventCommand>()))
            .Callback<object>(cmd => capturedCommand = (RegisterEventCommand)cmd)
            .Returns(ValueTask.CompletedTask);

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
        await publisher.PublishRegistrationEvent(cloudEvent, idempotencyId);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(idempotencyId, capturedCommand.IdempotencyId);
        Assert.Contains(cloudEvent.Id, capturedCommand.Payload);
    }

    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called with a null idempotency id.
    /// Expected result:
    ///   The RegisterEventCommand is sent with a null IdempotencyId.
    /// Success criteria:
    ///   The captured command's IdempotencyId is null.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_NullIdempotencyId_CommandHasNullId()
    {
        // Arrange
        RegisterEventCommand capturedCommand = null;

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<RegisterEventCommand>()))
            .Callback<object>(cmd => capturedCommand = (RegisterEventCommand)cmd)
            .Returns(ValueTask.CompletedTask);

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
        await publisher.PublishRegistrationEvent(cloudEvent, null);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand.IdempotencyId);
    }
}
