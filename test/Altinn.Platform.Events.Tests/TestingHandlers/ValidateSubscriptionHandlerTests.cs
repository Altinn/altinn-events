using System;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.Handlers;

public class ValidateSubscriptionHandlerTests
{
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly ValidateSubscriptionHandler _handler;

    public ValidateSubscriptionHandlerTests()
    {
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _handler = new ValidateSubscriptionHandler(_subscriptionServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsServiceAndSucceeds()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(subscription),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceReturnsError_ThrowsInvalidOperationException()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(502, "Webhook validation failed");

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));

        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
        Assert.Contains(serviceError.ErrorMessage, exception.Message);

        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(subscription),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceReturnsInternalServerError_ThrowsInvalidOperationException()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(500, "Internal server error");

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));

        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
        Assert.Contains("Internal server error", exception.Message);
    }

    [Fact]
    public async Task Handle_MultipleSubscriptions_CallsServiceForEach()
    {
        // Arrange
        var subscription1 = CreateTestSubscription(id: 1);
        var subscription2 = CreateTestSubscription(id: 2);
        var subscription3 = CreateTestSubscription(id: 3);

        var command1 = new ValidateSubscriptionCommand(subscription1);
        var command2 = new ValidateSubscriptionCommand(subscription2);
        var command3 = new ValidateSubscriptionCommand(subscription3);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(It.IsAny<Subscription>()))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command1);
        await _handler.Handle(command2);
        await _handler.Handle(command3);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.IsAny<Subscription>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsNullReferenceException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => _handler.Handle(null));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Handle_ServiceReturnsVariousErrorCodes_ThrowsInvalidOperationException(int errorCode)
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(errorCode, $"Error with code {errorCode}");

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));

        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
    }

    [Fact]
    public async Task Handle_SubscriptionWithDifferentEndpoints_PassesCorrectSubscriptionToService()
    {
        // Arrange
        var subscription1 = CreateTestSubscription(id: 1);
        subscription1.EndPoint = new Uri("https://endpoint1.com/webhook");

        var subscription2 = CreateTestSubscription(id: 2);
        subscription2.EndPoint = new Uri("https://endpoint2.com/webhook");

        var command1 = new ValidateSubscriptionCommand(subscription1);
        var command2 = new ValidateSubscriptionCommand(subscription2);

        Subscription capturedSubscription1 = null;
        Subscription capturedSubscription2 = null;

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(It.IsAny<Subscription>()))
            .Callback<Subscription>(s =>
            {
                if (capturedSubscription1 == null)
                {
                    capturedSubscription1 = s;
                }
                else
                {
                    capturedSubscription2 = s;
                }
            })
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command1);
        await _handler.Handle(command2);

        // Assert
        Assert.NotNull(capturedSubscription1);
        Assert.NotNull(capturedSubscription2);
        Assert.Equal(subscription1.EndPoint, capturedSubscription1.EndPoint);
        Assert.Equal(subscription2.EndPoint, capturedSubscription2.EndPoint);
    }

    [Fact]
    public async Task Handle_ServiceThrowsException_PropagatesException()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _handler.Handle(command));
    }

    [Theory]
    [InlineData("https://example.com/webhook")]
    [InlineData("https://api.example.com/events/webhook")]
    [InlineData("https://localhost:5000/api/webhooks/events")]
    public async Task Handle_DifferentEndpoints_PassesSubscriptionToService(string endpointUrl)
    {
        // Arrange
        var subscription = CreateTestSubscription();
        subscription.EndPoint = new Uri(endpointUrl);
        var command = new ValidateSubscriptionCommand(subscription);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(It.IsAny<Subscription>()))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.EndPoint.ToString() == endpointUrl)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionWithDifferentIds_PassesToService()
    {
        // Arrange
        var subscription1 = CreateTestSubscription(id: 123);
        var subscription2 = CreateTestSubscription(id: 456);
        var command1 = new ValidateSubscriptionCommand(subscription1);
        var command2 = new ValidateSubscriptionCommand(subscription2);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(It.IsAny<Subscription>()))
            .ReturnsAsync((ServiceError)null);

        // Act
        await _handler.Handle(command1);
        await _handler.Handle(command2);

        // Assert
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.Id == 123)),
            Times.Once);
        _subscriptionServiceMock.Verify(
            x => x.SendAndValidate(It.Is<Subscription>(s => s.Id == 456)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceReturnsErrorWithEmptyMessage_ThrowsWithDefaultMessage()
    {
        // Arrange
        var subscription = CreateTestSubscription();
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
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError(500, null);

        _subscriptionServiceMock
            .Setup(x => x.SendAndValidate(subscription))
            .ReturnsAsync(serviceError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));
        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
    }

    private static Subscription CreateTestSubscription(int id = 1)
    {
        return new Subscription
        {
            Id = id,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://example.com/webhook"),
            SourceFilter = new Uri("https://ttd.apps.altinn.no/ttd/apps-test"),
            CreatedBy = "/user/12345",
            Created = DateTime.UtcNow
        };
    }
}
