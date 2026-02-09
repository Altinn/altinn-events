using System;
using System.Linq;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Handlers;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.Handlers;

public class ValidateSubscriptionHandlerEdgeCaseTests
{
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<ITraceLogService> _traceLogServiceMock;
    private readonly Mock<IWebhookService> _webhookServiceMock;
    private readonly Mock<ILogger<ValidateSubscriptionHandler>> _loggerMock;
    private readonly ValidateSubscriptionHandler _handler;

    public ValidateSubscriptionHandlerEdgeCaseTests()
    {
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _traceLogServiceMock = new Mock<ITraceLogService>();
        _webhookServiceMock = new Mock<IWebhookService>();
        _loggerMock = new Mock<ILogger<ValidateSubscriptionHandler>>();

        _handler = new ValidateSubscriptionHandler(
            _subscriptionServiceMock.Object,
            _traceLogServiceMock.Object,
            _webhookServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => _handler.Handle(null));
    }

    [Fact]
    public async Task Handle_CommandWithNullSubscription_ThrowsNullReferenceException()
    {
        // Arrange
        var command = new ValidateSubscriptionCommand(null);

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => _handler.Handle(command));
    }

    [Fact]
    public async Task Handle_SubscriptionWithEmptyConsumer_HandlesCorrectly()
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

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        // Act
        await _handler.Handle(command);

        // Assert
        _webhookServiceMock.Verify(
            x => x.SendAsync(It.Is<CloudEventEnvelope>(e => e.Consumer == string.Empty)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleSequentialCalls_EachCreatesUniqueCloudEvent()
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

        var cloudEventIds = new System.Collections.Generic.List<string>();

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Callback<CloudEventEnvelope>(e => cloudEventIds.Add(e.CloudEvent.Id))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        // Act
        await _handler.Handle(command);
        await _handler.Handle(command);
        await _handler.Handle(command);

        // Assert
        Assert.Equal(3, cloudEventIds.Count);
        Assert.Equal(3, cloudEventIds.Distinct().Count()); // All IDs should be unique
    }
}
