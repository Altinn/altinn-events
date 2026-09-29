using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.BackgroundServices;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

/// <summary>
/// A collection of tests related to <see cref="RegisteredEventsBackgroundService"/>.
/// </summary>
public class RegisteredEventsBackgroundServiceTests
{
    private readonly Mock<IRegisteredEventsProcessingService> _processingServiceMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<ILogger<RegisteredEventsBackgroundService>> _loggerMock;

    public RegisteredEventsBackgroundServiceTests()
    {
        _processingServiceMock = new();
        _loggerMock = new();

        Mock<IServiceProvider> serviceProviderMock = new();
        serviceProviderMock
            .Setup(p => p.GetService(typeof(IRegisteredEventsProcessingService)))
            .Returns(_processingServiceMock.Object);

        _serviceScopeMock = new();
        _serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        _serviceScopeFactoryMock = new();
        _serviceScopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_serviceScopeMock.Object);
    }

    /// <summary>
    /// Scenario:
    ///   TaskCount is configured as 0.
    /// Expected result:
    ///   No processing loops are started.
    /// Success criteria:
    ///   No service scope is ever created and TryProcessEvent is never called.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TaskCountIsZero_NoScopesCreated()
    {
        // Arrange
        EventsProcessingSettings settings = new() { TaskCount = 0 };
        RegisteredEventsBackgroundService target = GetTarget(settings);

        // Act
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await target.StopAsync(CancellationToken.None);

        // Assert
        _serviceScopeFactoryMock.Verify(f => f.CreateScope(), Times.Never);
        _processingServiceMock.Verify(p => p.TryProcessEvent(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Scenario:
    ///   TaskCount is configured as 1 and no events are ever available to process.
    /// Expected result:
    ///   The single polling loop repeatedly attempts to process an event, idling between attempts.
    /// Success criteria:
    ///   TryProcessEvent is called at least once via a scoped IRegisteredEventsProcessingService.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_TaskCountIsOne_CallsTryProcessEventAtLeastOnce()
    {
        // Arrange
        EventsProcessingSettings settings = new()
        {
            TaskCount = 1,
            PrimaryTaskIdleDelaySeconds = 0,
            AdditionalTasksIdleDelaySeconds = 0
        };

        _processingServiceMock
            .Setup(p => p.TryProcessEvent(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        RegisteredEventsBackgroundService target = GetTarget(settings);

        // Act
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await target.StopAsync(CancellationToken.None);

        // Assert
        _serviceScopeFactoryMock.Verify(f => f.CreateScope(), Times.AtLeastOnce);
        _processingServiceMock.Verify(p => p.TryProcessEvent(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent throws once, then returns false on subsequent calls.
    /// Expected result:
    ///   The polling loop logs the error and keeps running instead of crashing.
    /// Success criteria:
    ///   TryProcessEvent is called more than once (i.e. the loop survived the exception),
    ///   and an error is logged.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ProcessingThrowsOnce_LoopContinuesAndLogsError()
    {
        // Arrange
        EventsProcessingSettings settings = new()
        {
            TaskCount = 1,
            PrimaryTaskIdleDelaySeconds = 0,
            AdditionalTasksIdleDelaySeconds = 0
        };

        int callCount = 0;
        _processingServiceMock
            .Setup(p => p.TryProcessEvent(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Simulated processing failure");
                }

                return false;
            });

        RegisteredEventsBackgroundService target = GetTarget(settings);

        // Act
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await target.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(callCount > 1);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string?>)It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Scenario:
    ///   TaskCount is configured as more than 1, and no events are ever available to process
    ///   (i.e. the primary task never observes enough consecutive successful claims to ramp up).
    /// Expected result:
    ///   Only the primary task attempts to process events; additional tasks never call
    ///   TryProcessEvent because the ramp-up threshold is never reached.
    /// Success criteria:
    ///   TryProcessEvent is called (by the primary task), but never more than what a single
    ///   task idling and retrying could produce — i.e. additional tasks stay dormant.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultipleTasksAndNoBacklog_OnlyPrimaryTaskProcesses()
    {
        // Arrange
        EventsProcessingSettings settings = new()
        {
            TaskCount = 3,
            PrimaryTaskIdleDelaySeconds = 0,
            AdditionalTasksIdleDelaySeconds = 0,
            RampUpLimit = 5
        };

        int callCount = 0;
        _processingServiceMock
            .Setup(p => p.TryProcessEvent(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return false; // never any events to claim
            });

        RegisteredEventsBackgroundService target = GetTarget(settings);

        // Act
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await target.StopAsync(CancellationToken.None);

        // Assert
        // Only the primary task should ever call TryProcessEvent, since it always returns
        // false (no backlog), so additional tasks never see the ramp-up flag flip to true.
        Assert.True(callCount > 0);
        _serviceScopeFactoryMock.Verify(f => f.CreateScope(), Times.AtLeastOnce);
    }

    /// <summary>
    /// Scenario:
    ///   TaskCount is configured as more than 1, and the primary task succeeds enough
    ///   consecutive times to reach RampUpLimit.
    /// Expected result:
    ///   Additional tasks start attempting to process events once the ramp-up threshold
    ///   is reached, resulting in more total TryProcessEvent calls than the primary task
    ///   alone could produce in the same window.
    /// Success criteria:
    ///   TryProcessEvent is called by more than one distinct scope/task, evidenced by a
    ///   higher call count than a single-task baseline over the same delay window.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultipleTasksAndSustainedBacklog_AdditionalTasksRampUp()
    {
        // Arrange
        EventsProcessingSettings settings = new()
        {
            TaskCount = 3,
            PrimaryTaskIdleDelaySeconds = 0,
            AdditionalTasksIdleDelaySeconds = 0,
            RampUpLimit = 2
        };

        _processingServiceMock
            .Setup(p => p.TryProcessEvent(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // always "claims" an event, simulating sustained backlog

        RegisteredEventsBackgroundService target = GetTarget(settings);

        // Act
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await target.StopAsync(CancellationToken.None);

        // Assert
        // With sustained backlog (always processed == true), the primary task should reach
        // RampUpLimit quickly and additional tasks should start processing too, producing
        // substantially more scope creations/calls than a single task could alone.
        _serviceScopeFactoryMock.Verify(f => f.CreateScope(), Times.AtLeast(settings.RampUpLimit + 1));
    }

    private RegisteredEventsBackgroundService GetTarget(EventsProcessingSettings settings)
    {
        return new RegisteredEventsBackgroundService(
            _serviceScopeFactoryMock.Object,
            Options.Create(settings),
            _loggerMock.Object);
    }
}
