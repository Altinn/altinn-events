using System;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Handlers;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.Handlers;

public class ValidateSubscriptionHandlerTests
{
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<ITraceLogService> _traceLogServiceMock;
    private readonly Mock<IWebhookService> _webhookServiceMock;
    private readonly Mock<ILogger<ValidateSubscriptionHandler>> _loggerMock;
    private readonly ValidateSubscriptionHandler _handler;

    public ValidateSubscriptionHandlerTests()
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
    public async Task Handle_ValidCommand_SendsWebhookAndMarksSubscriptionValid()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command);

        // Assert
        _webhookServiceMock.Verify(
            x => x.SendAsync(It.Is<CloudEventEnvelope>(e =>
                e.SubscriptionId == subscription.Id &&
                e.Endpoint == subscription.EndPoint &&
                e.Consumer == subscription.Consumer &&
                e.CloudEvent.Type == "platform.events.validatesubscription")),
            Times.Once);

        _subscriptionServiceMock.Verify(
            x => x.SetValidSubscription(subscription.Id),
            Times.Once);

        _traceLogServiceMock.Verify(
            x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                subscription,
                TraceLogActivity.EndpointValidationSuccess),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully validated subscription")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesCorrectCloudEvent()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        CloudEventEnvelope capturedEnvelope = null;

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Callback<CloudEventEnvelope>(e => capturedEnvelope = e)
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command);

        // Assert
        Assert.NotNull(capturedEnvelope);
        Assert.NotNull(capturedEnvelope.CloudEvent);

        var cloudEvent = capturedEnvelope.CloudEvent;
        Assert.NotNull(cloudEvent.Id);
        Assert.Equal("platform.events.validatesubscription", cloudEvent.Type);
        Assert.Equal(new Uri($"urn:altinn:events:subscriptions:{subscription.Id}"), cloudEvent.Source);
    }

    [Fact]
    public async Task Handle_SetValidSubscriptionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var serviceError = new ServiceError((int)System.Net.HttpStatusCode.InternalServerError, "Database error");

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, serviceError));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationFailed))
            .ReturnsAsync(string.Empty);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command));

        Assert.Contains($"Failed to validate subscription {subscription.Id}", exception.Message);
        Assert.Contains(serviceError.ErrorMessage, exception.Message);

        _traceLogServiceMock.Verify(
            x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                subscription,
                TraceLogActivity.EndpointValidationFailed),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to mark subscription")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WebhookSendFails_ThrowsHttpRequestExceptionAndLogsFailure()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var httpException = new HttpRequestException("Connection timeout");

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .ThrowsAsync(httpException);

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationFailed))
            .ReturnsAsync(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _handler.Handle(command));

        _traceLogServiceMock.Verify(
            x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                subscription,
                TraceLogActivity.EndpointValidationFailed),
            Times.Once);

        _subscriptionServiceMock.Verify(
            x => x.SetValidSubscription(It.IsAny<int>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Webhook validation failed")),
                httpException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnexpectedException_ThrowsAndLogsFailure()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        var exception = new Exception("Unexpected error");

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .ThrowsAsync(exception);

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationFailed))
            .ReturnsAsync(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command));

        _traceLogServiceMock.Verify(
            x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                subscription,
                TraceLogActivity.EndpointValidationFailed),
            Times.Once);

        _subscriptionServiceMock.Verify(
            x => x.SetValidSubscription(It.IsAny<int>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error occurred while validating subscription")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsInformationMessages()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Handling ValidateSubscriptionCommand")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully validated subscription")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesEnvelopeWithCorrectProperties()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);
        CloudEventEnvelope capturedEnvelope = null;

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Callback<CloudEventEnvelope>(e => capturedEnvelope = e)
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command);

        // Assert
        Assert.NotNull(capturedEnvelope);
        Assert.Equal(subscription.Consumer, capturedEnvelope.Consumer);
        Assert.Equal(subscription.EndPoint, capturedEnvelope.Endpoint);
        Assert.Equal(subscription.Id, capturedEnvelope.SubscriptionId);
    }

    [Fact]
    public async Task Handle_TraceLogServiceFails_DoesNotAffectMainFlow()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var command = new ValidateSubscriptionCommand(subscription);

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ThrowsAsync(new Exception("Trace log failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command));

        // Verify webhook was sent and subscription was marked valid before trace log failed
        _webhookServiceMock.Verify(
            x => x.SendAsync(It.IsAny<CloudEventEnvelope>()),
            Times.Once);

        _subscriptionServiceMock.Verify(
            x => x.SetValidSubscription(subscription.Id),
            Times.Once);
    }

    [Theory]
    [InlineData("https://example.com/webhook")]
    [InlineData("https://api.example.com/events/webhook")]
    [InlineData("https://localhost:5000/api/webhooks/events")]
    public async Task Handle_DifferentEndpoints_HandlesCorrectly(string endpointUrl)
    {
        // Arrange
        var subscription = CreateTestSubscription();
        subscription.EndPoint = new Uri(endpointUrl);
        var command = new ValidateSubscriptionCommand(subscription);

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(subscription.Id))
            .ReturnsAsync((subscription, (ServiceError)null));

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command);

        // Assert
        _webhookServiceMock.Verify(
            x => x.SendAsync(It.Is<CloudEventEnvelope>(e =>
                e.Endpoint.ToString() == endpointUrl)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionWithDifferentIds_CreatesUniqueCloudEvents()
    {
        // Arrange
        var subscription1 = CreateTestSubscription(id: 123);
        var subscription2 = CreateTestSubscription(id: 456);
        var command1 = new ValidateSubscriptionCommand(subscription1);
        var command2 = new ValidateSubscriptionCommand(subscription2);

        CloudEventEnvelope envelope1 = null;
        CloudEventEnvelope envelope2 = null;

        _webhookServiceMock
            .Setup(x => x.SendAsync(It.IsAny<CloudEventEnvelope>()))
            .Callback<CloudEventEnvelope>(e =>
            {
                if (envelope1 == null)
                { 
                    envelope1 = e;
                }
                else
                { 
                    envelope2 = e;
                }
            })
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock
            .Setup(x => x.SetValidSubscription(It.IsAny<int>()))
            .ReturnsAsync((int id) =>
            {
                var sub = id == 123 ? subscription1 : subscription2;
                return (sub, (ServiceError)null);
            });

        _traceLogServiceMock
            .Setup(x => x.CreateLogEntryWithSubscriptionDetails(
                It.IsAny<CloudEvent>(),
                It.IsAny<Subscription>(),
                TraceLogActivity.EndpointValidationSuccess))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Handle(command1);
        await _handler.Handle(command2);

        // Assert
        Assert.NotNull(envelope1);
        Assert.NotNull(envelope2);
        Assert.NotEqual(envelope1.CloudEvent.Id, envelope2.CloudEvent.Id);
        Assert.Equal(123, envelope1.SubscriptionId);
        Assert.Equal(456, envelope2.SubscriptionId);
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
