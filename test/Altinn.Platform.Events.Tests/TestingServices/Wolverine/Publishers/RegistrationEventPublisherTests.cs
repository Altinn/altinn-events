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
    ///   PublishRegistrationEvent is called with a cloud event and an idempotency key.
    /// Expected result:
    ///   The RegisterEventCommand sent to the message bus carries the same idempotency key and the serialized payload.
    /// Success criteria:
    ///   IMessageBus.SendAsync is called once with a RegisterEventCommand whose IdempotencyKey matches and whose
    ///   Payload deserializes back to the original cloud event id.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_WithIdempotencyKey_CommandCarriesIdAndPayload()
    {
        // Arrange
        Guid idempotencyKey = Guid.Parse("d1525c79-cda8-4fef-b95c-feb3e7be89ec");
        RegisterEventCommand capturedCommand = null;

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<RegisterEventCommand>(), It.IsAny<DeliveryOptions>()))
            .Callback<RegisterEventCommand, DeliveryOptions>((cmd, _) => capturedCommand = cmd)
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
        await publisher.PublishRegistrationEvent(cloudEvent, idempotencyKey);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(idempotencyKey, capturedCommand.IdempotencyKey);
        Assert.Contains(cloudEvent.Id, capturedCommand.Payload);
    }

    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called with a null idempotency key.
    /// Expected result:
    ///   The RegisterEventCommand is sent with a null IdempotencyKey.
    /// Success criteria:
    ///   The captured command's IdempotencyKey is null.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_NullIdempotencyKey_CommandHasNullId()
    {
        // Arrange
        RegisterEventCommand capturedCommand = null;

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<RegisterEventCommand>(), It.IsAny<DeliveryOptions>()))
            .Callback<RegisterEventCommand, DeliveryOptions>((cmd, _) => capturedCommand = cmd)
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
        Assert.Null(capturedCommand.IdempotencyKey);
    }
}
