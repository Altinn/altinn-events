using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Wolverine.Publishers;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Publishers;

/// <summary>
/// A collection of tests related to <see cref="StorageQueueRegistrationEventPublisher"/>.
/// </summary>
public class StorageQueueRegistrationEventPublisherTests
{
    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called with a cloud event.
    /// Expected result:
    ///   Event is enqueued to the Storage Queue.
    /// Success criteria:
    ///   QueueClient.EnqueueRegistration is called once.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_EnqueuesSuccessfully()
    {
        // Arrange
        Mock<IEventsQueueClient> queueMock = new();
        queueMock.Setup(q => q.EnqueueRegistration(It.IsAny<string>()))
            .ReturnsAsync(new QueuePostReceipt { Success = true });

        var publisher = new StorageQueueRegistrationEventPublisher(queueMock.Object);

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
        queueMock.Verify(q => q.EnqueueRegistration(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Scenario:
    ///   PublishRegistrationEvent is called and the queue fails.
    /// Expected result:
    ///   Error is thrown.
    /// Success criteria:
    ///   The receipt's exception is thrown.
    /// </summary>
    [Fact]
    public async Task PublishRegistrationEvent_QueueFails_ThrowsException()
    {
        // Arrange
        Mock<IEventsQueueClient> queueMock = new();
        queueMock.Setup(q => q.EnqueueRegistration(It.IsAny<string>()))
            .ReturnsAsync(new QueuePostReceipt { Success = false, Exception = new Exception("Queue failed") });

        var publisher = new StorageQueueRegistrationEventPublisher(queueMock.Object);

        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "instance.created",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/apps-test/"),
            Time = DateTime.Now,
            Subject = "/party/456456"
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => publisher.PublishRegistrationEvent(cloudEvent));
    }
}
