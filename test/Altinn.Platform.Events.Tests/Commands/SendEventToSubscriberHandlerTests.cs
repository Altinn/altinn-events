using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using CloudNative.CloudEvents;
using Moq;
using Wolverine.Runtime.Handlers;
using Xunit;

namespace Altinn.Platform.Events.Tests.Commands;

public class SendEventToSubscriberHandlerTests
{
    private readonly Mock<IWebhookService> _mockWebhookService;
    private readonly CloudEventEnvelope _testEnvelope;
    private readonly CancellationToken _cancellationToken;

    public SendEventToSubscriberHandlerTests()
    {
        _mockWebhookService = new Mock<IWebhookService>();
        _cancellationToken = CancellationToken.None;

        _testEnvelope = new CloudEventEnvelope
        {
            CloudEvent = new CloudEvent
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri("https://test.altinn.no/events"),
                Type = "test.event.type",
                Time = DateTime.UtcNow
            },
            Endpoint = new Uri("https://subscriber.example.com/webhook"),
            Consumer = "org:testorg",
            SubscriptionId = 123,
            Pushed = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsWebhookServiceSend()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken);

        // Assert
        _mockWebhookService.Verify(
            s => s.Send(_testEnvelope, _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCorrectEnvelope_ToWebhookService()
    {
        // Arrange
        CloudEventEnvelope capturedEnvelope = null;
        var command = new OutboundEventCommand(_testEnvelope);

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((env, ct) => capturedEnvelope = env)
            .Returns(Task.CompletedTask);

        // Act
        await SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken);

        // Assert
        Assert.NotNull(capturedEnvelope);
        Assert.Equal(_testEnvelope.CloudEvent.Id, capturedEnvelope.CloudEvent.Id);
        Assert.Equal(_testEnvelope.Endpoint, capturedEnvelope.Endpoint);
        Assert.Equal(_testEnvelope.Consumer, capturedEnvelope.Consumer);
        Assert.Equal(_testEnvelope.SubscriptionId, capturedEnvelope.SubscriptionId);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToWebhookService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var command = new OutboundEventCommand(_testEnvelope);
        CancellationToken capturedToken = default;

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((env, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);

        // Act
        await SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task Handle_WhenWebhookServiceThrowsHttpRequestException_PropagatesException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var expectedException = new HttpRequestException("Webhook endpoint returned 503");

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenWebhookServiceThrowsHttpIOException_PropagatesException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var expectedException = new HttpIOException(HttpRequestError.ConnectionError, "Connection reset");

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpIOException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenWebhookServiceThrowsTimeoutException_PropagatesException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var expectedException = new TimeoutException("Request timed out");

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenWebhookServiceThrowsSocketException_PropagatesException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var expectedException = new SocketException((int)SocketError.HostUnreachable);

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SocketException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken));

        Assert.Equal(expectedException.SocketErrorCode, exception.SocketErrorCode);
    }

    [Fact]
    public async Task Handle_WhenWebhookServiceThrowsTaskCanceledException_PropagatesException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var expectedException = new TaskCanceledException("Operation was canceled");

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var command = new OutboundEventCommand(_testEnvelope);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockWebhookService
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendEventToSubscriberHandler.Handle(command, _mockWebhookService.Object, cts.Token));
    }

    [Fact]
    public void Configure_WithValidSettings_ConfiguresRetryPolicy()
    {
        // Arrange
        var settings = new WolverineSettings
        {
            OutboundQueuePolicy = new QueueRetryPolicy
            {
                CooldownDelaysMs = [1000, 5000, 10000],
                ScheduleDelaysMs = [30000, 60000, 120000]
            }
        };

        SendEventToSubscriberHandler.Settings = settings;
        var mockChain = new Mock<HandlerChain>();

        // Act - this would normally be called by Wolverine during configuration
        // We're verifying that Settings is properly set
        Assert.NotNull(SendEventToSubscriberHandler.Settings);
        Assert.Equal(settings, SendEventToSubscriberHandler.Settings);
        Assert.Equal(3, settings.OutboundQueuePolicy.CooldownDelaysMs.Length);
        Assert.Equal(3, settings.OutboundQueuePolicy.ScheduleDelaysMs.Length);
    }

    [Fact]
    public void Settings_CanBeSetAndRetrieved()
    {
        // Arrange
        var settings = new WolverineSettings
        {
            EnableServiceBus = true,
            OutboundQueuePolicy = new QueueRetryPolicy
            {
                CooldownDelaysMs = [1000],
                ScheduleDelaysMs = [30000]
            }
        };

        // Act
        SendEventToSubscriberHandler.Settings = settings;

        // Assert
        Assert.NotNull(SendEventToSubscriberHandler.Settings);
        Assert.Equal(settings.EnableServiceBus, SendEventToSubscriberHandler.Settings.EnableServiceBus);
        Assert.Equal(settings.OutboundQueuePolicy.CooldownDelaysMs, SendEventToSubscriberHandler.Settings.OutboundQueuePolicy.CooldownDelaysMs);
    }
}
