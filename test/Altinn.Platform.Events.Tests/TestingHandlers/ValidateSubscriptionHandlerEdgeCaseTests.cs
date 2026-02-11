using System;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.Handlers;

public class ValidateSubscriptionHandlerEdgeCaseTests
{
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly ValidateSubscriptionHandler _handler;

    public ValidateSubscriptionHandlerEdgeCaseTests()
    {
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _handler = new ValidateSubscriptionHandler(_subscriptionServiceMock.Object);
    }

    [Fact]
    public async Task Handle_SubscriptionWithEmptyConsumer_PassesToService()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = string.Empty,
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.Consumer == string.Empty)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionWithNullEndpoint_PassesToService()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = "/org/ttd",
            EndPoint = null,
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.EndPoint == null)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleSequentialCalls_EachCallsService()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);
        await _handler.Handle(command);
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(subscription),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ServiceReturnsErrorWithEmptyMessage_ThrowsWithDefaultMessage()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(500, string.Empty);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));
        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
    }

    [Fact]
    public async Task Handle_ServiceReturnsErrorWithNullMessage_ThrowsWithDefaultMessage()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(500, null);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));
        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Handle_SubscriptionWithVariousIds_PassesToService(int subscriptionId)
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.Id == subscriptionId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrentCalls_AllCallsProcessed()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = 1,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        var tasks = Enumerable.Range(0, 5).Select(_ => _handler.Handle(command));
        await Task.WhenAll(tasks);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(subscription),
            Times.Exactly(5));
    }

    [Fact]
    public async Task Handle_ServiceReturnsError_ErrorMessageIncludesSubscriptionId()
    {
        // Arrange
        var subscriptionId = 12345;
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            Created = DateTime.UtcNow
        };
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(500, "Test error");

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));
        Assert.Contains(subscriptionId.ToString(), exception.Message);
    }
}
