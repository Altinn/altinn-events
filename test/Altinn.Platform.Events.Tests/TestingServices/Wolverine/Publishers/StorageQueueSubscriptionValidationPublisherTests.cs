using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Wolverine.Publishers;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Publishers;

/// <summary>
/// A collection of tests related to <see cref="StorageQueueSubscriptionValidationPublisher"/>.
/// </summary>
public class StorageQueueSubscriptionValidationPublisherTests
{
    /// <summary>
    /// Scenario:
    ///   PublishValidationEvent is called and the queue fails.
    /// Expected result:
    ///   Error is thrown.
    /// Success criteria:
    ///   The receipt's exception is thrown.
    /// </summary>
    [Fact]
    public async Task PublishValidationEvent_QueueFails_ThrowsException()
    {
        // Arrange
        Mock<IEventsQueueClient> queueMock = new();
        queueMock.Setup(q => q.EnqueueSubscriptionValidation(It.IsAny<string>()))
            .ReturnsAsync(new QueuePostReceipt { Success = false, Exception = new Exception("Queue failed") });

        var publisher = new StorageQueueSubscriptionValidationPublisher(queueMock.Object);

        Subscription subscription = new()
        {
            Id = 100,
            SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => publisher.PublishValidationEvent(subscription));
    }

    /// <summary>
    /// Scenario:
    ///   PublishValidationEvent is called with a subscription.
    /// Expected result:
    ///   Event is enqueued to the Storage Queue.
    /// Success criteria:
    ///   QueueClient.EnqueueSubscriptionValidation is called once.
    /// </summary>
    [Fact]
    public async Task PublishValidationEvent_EnqueuesSuccessfully()
    {
        // Arrange
        Mock<IEventsQueueClient> queueMock = new();
        queueMock.Setup(q => q.EnqueueSubscriptionValidation(It.IsAny<string>()))
            .ReturnsAsync(new QueuePostReceipt { Success = true });

        var publisher = new StorageQueueSubscriptionValidationPublisher(queueMock.Object);

        Subscription subscription = new()
        {
            Id = 100,
            SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
        };

        // Act
        await publisher.PublishValidationEvent(subscription);

        // Assert
        queueMock.Verify(q => q.EnqueueSubscriptionValidation(It.IsAny<string>()), Times.Once);
    }
}
