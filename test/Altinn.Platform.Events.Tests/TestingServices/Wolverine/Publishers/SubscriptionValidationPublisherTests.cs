using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Wolverine.Commands;
using Altinn.Platform.Events.Wolverine.Publishers;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Publishers;

/// <summary>
/// A collection of tests related to <see cref="SubscriptionValidationPublisher"/>.
/// </summary>
public class SubscriptionValidationPublisherTests
{
    /// <summary>
    /// Scenario:
    ///   PublishValidationEvent is called with a subscription.
    /// Expected result:
    ///   The subscription is sent to the message bus as a ValidateSubscriptionCommand.
    /// Success criteria:
    ///   IMessageBus.SendAsync is called once with a ValidateSubscriptionCommand.
    /// </summary>
    [Fact]
    public async Task PublishValidationEvent_ValidSubscription_SendsToMessageBus()
    {
        // Arrange
        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.SendAsync(It.IsAny<ValidateSubscriptionCommand>())).Returns(ValueTask.CompletedTask);

        var publisher = new SubscriptionValidationPublisher(busMock.Object);

        Subscription subscription = new()
        {
            Id = 42,
            SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
        };

        // Act
        await publisher.PublishValidationEvent(subscription);

        // Assert
        busMock.Verify(b => b.SendAsync(It.IsAny<ValidateSubscriptionCommand>()), Times.Once);
    }
}
