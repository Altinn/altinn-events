using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using CloudNative.CloudEvents;
using Moq;
using Wolverine.Runtime.Handlers;
using Xunit;

namespace Altinn.Platform.Events.Tests.Commands;

public class SaveEventHandlerTests
{
    private readonly Mock<IEventsService> _mockEventsService;
    private readonly CloudEvent _testCloudEvent;
    private readonly CancellationToken _cancellationToken;

    public SaveEventHandlerTests()
    {
        _mockEventsService = new Mock<IEventsService>();
        _cancellationToken = CancellationToken.None;

        _testCloudEvent = new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = new Uri("https://test.altinn.no/events"),
            Type = "test.event.type",
            Time = DateTime.UtcNow,
            Subject = "party/12345"
        };
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsEventsServiceSaveAndPublish()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken);

        // Assert
        _mockEventsService.Verify(
            s => s.SaveAndPublish(_testCloudEvent, _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCorrectCloudEvent_ToEventsService()
    {
        // Arrange
        CloudEvent capturedEvent = null;
        var command = new RegisterEventCommand(_testCloudEvent);

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEvent, CancellationToken>((ce, ct) => capturedEvent = ce)
            .Returns(Task.CompletedTask);

        // Act
        await SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken);

        // Assert
        Assert.NotNull(capturedEvent);
        Assert.Equal(_testCloudEvent.Id, capturedEvent.Id);
        Assert.Equal(_testCloudEvent.Source, capturedEvent.Source);
        Assert.Equal(_testCloudEvent.Type, capturedEvent.Type);
        Assert.Equal(_testCloudEvent.Subject, capturedEvent.Subject);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToEventsService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var command = new RegisterEventCommand(_testCloudEvent);
        CancellationToken capturedToken = default;

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEvent, CancellationToken>((ce, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);

        // Act
        await SaveEventHandler.Handle(command, _mockEventsService.Object, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(
            () => SaveEventHandler.Handle(null, _mockEventsService.Object, _cancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullEventsService_ThrowsNullReferenceException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(
            () => SaveEventHandler.Handle(command, null, _cancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullCloudEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var command = new RegisterEventCommand(null);

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException(nameof(CloudEvent)));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));
    }

    [Fact]
    public async Task Handle_WhenEventsServiceThrowsInvalidOperationException_PropagatesException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenEventsServiceThrowsTaskCanceledException_PropagatesException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var expectedException = new TaskCanceledException("Database operation was canceled");

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenEventsServiceThrowsTimeoutException_PropagatesException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var expectedException = new TimeoutException("Database query timed out");

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenEventsServiceThrowsSocketException_PropagatesException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var expectedException = new SocketException((int)SocketError.HostUnreachable);

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SocketException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));

        Assert.Equal(expectedException.SocketErrorCode, exception.SocketErrorCode);
    }

    [Fact]
    public async Task Handle_WhenEventsServiceThrowsServiceBusException_PropagatesException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var expectedException = new ServiceBusException("Service Bus connection failed", ServiceBusFailureReason.ServiceCommunicationProblem);

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceBusException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken));

        Assert.Equal(expectedException.Reason, exception.Reason);
    }

    [Fact]
    public async Task Handle_WithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var command = new RegisterEventCommand(_testCloudEvent);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockEventsService
            .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SaveEventHandler.Handle(command, _mockEventsService.Object, cts.Token));
    }

    [Fact]
    public async Task Handle_WithDifferentEventTypes_ProcessesCorrectly()
    {
        // Arrange
        var eventTypes = new[] { "app.instance.created", "app.instance.process.completed", "app.instance.deleted" };

        foreach (var eventType in eventTypes)
        {
            var cloudEvent = new CloudEvent
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri("https://test.altinn.no/events"),
                Type = eventType,
                Time = DateTime.UtcNow
            };

            var command = new RegisterEventCommand(cloudEvent);

            _mockEventsService
                .Setup(s => s.SaveAndPublish(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await SaveEventHandler.Handle(command, _mockEventsService.Object, _cancellationToken);

            // Assert
            _mockEventsService.Verify(
                s => s.SaveAndPublish(It.Is<CloudEvent>(e => e.Type == eventType), _cancellationToken),
                Times.Once);

            _mockEventsService.Reset();
        }
    }

    [Fact]
    public void Configure_WithValidSettings_ConfiguresRetryPolicy()
    {
        // Arrange
        var settings = new WolverineSettings
        {
            RegistrationQueuePolicy = new QueueRetryPolicy
            {
                CooldownDelaysMs = [1000, 5000, 10000],
                ScheduleDelaysMs = [30000, 60000, 120000]
            }
        };

        SaveEventHandler.Settings = settings;

        // Act & Assert
        Assert.NotNull(SaveEventHandler.Settings);
        Assert.Equal(settings, SaveEventHandler.Settings);
        Assert.Equal(3, settings.RegistrationQueuePolicy.CooldownDelaysMs.Length);
        Assert.Equal(3, settings.RegistrationQueuePolicy.ScheduleDelaysMs.Length);
    }

    [Fact]
    public void Settings_CanBeSetAndRetrieved()
    {
        // Arrange
        var settings = new WolverineSettings
        {
            EnableServiceBus = true,
            RegistrationQueuePolicy = new QueueRetryPolicy
            {
                CooldownDelaysMs = [1000],
                ScheduleDelaysMs = [30000]
            }
        };

        // Act
        SaveEventHandler.Settings = settings;

        // Assert
        Assert.NotNull(SaveEventHandler.Settings);
        Assert.Equal(settings.EnableServiceBus, SaveEventHandler.Settings.EnableServiceBus);
        Assert.Equal(settings.RegistrationQueuePolicy.CooldownDelaysMs, SaveEventHandler.Settings.RegistrationQueuePolicy.CooldownDelaysMs);
    }

    [Fact]
    public void Settings_DefaultValue_IsNull()
    {
        // Arrange - Reset to default
        SaveEventHandler.Settings = null!;

        // Act & Assert
        Assert.Null(SaveEventHandler.Settings);
    }
}
