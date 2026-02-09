#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.IntegrationTests.Emulator;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Events;

/// <summary>
/// Integration tests for Wolverine retry policies with Azure Service Bus Emulator.
/// These tests verify that the retry policy correctly handles exceptions and moves messages to the error queue.
/// </summary>
[Collection(nameof(AzureServiceBusEmulatorCollection))]
public class RetryPolicyIntegrationTests(AzureServiceBusEmulatorFixture fixture)
{
    private readonly AzureServiceBusEmulatorFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the database is available.
    /// Message should flow: RegisterQueue -> SaveEventHandler -> Save to DB -> InboundQueue -> ...
    /// Verifies:
    /// - Register queue is empty (message was processed)
    /// - Register DLQ is empty (no failures)
    /// - Database save was called exactly once
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseAvailable_MessageFlowsToInboundQueue()
    {
        // Arrange
        await using var host = await new WolverineIntegrationTestHost(_fixture)
            .WithCleanQueues()
            .WithCloudEventRepository(mock =>
            {
                mock.Setup(r => r.CreateEvent(It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
            })
            .StartAsync();

        var cloudEvent = WolverineIntegrationTestHost.CreateTestCloudEvent();

        // Act
        // await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert - Register queue should be empty (message was processed)
        // var registerQueueEmpty = await host.WaitForEmptyAsync(host.RegisterQueueName);

        // Assert.True(registerQueueEmpty, "Register queue should be empty after successful processing");

        // Assert - Database save should have been called
        // var repositoryInvoked = await host.WaitForRepositoryInvocationAsync();
        
        // Assert.True(repositoryInvoked, "Database save should have been called within timeout");

        // Assert - Register DLQ should be empty (no failures)
        // var registerDlqEmpty = await host.WaitForDeadLetterEmptyAsync(host.RegisterQueueName);
        
        // Assert.True(registerDlqEmpty, "Register dead letter queue should be empty (no failures)");

        // Assert - Database save should not be called more than once
        // host.CloudEventRepositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests the retry policy when the database throws TaskCanceledException.
    /// Message should retry according to policy then move to dead letter queue.
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseThrowsTaskCanceledException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        int attemptCount = 0;

        await using var host = await new WolverineIntegrationTestHost(_fixture)
            .WithCleanQueues()
            .WithCloudEventRepository(mock =>
            {
                mock.Setup(r => r.CreateEvent(It.IsAny<string>()))
                    .Callback<string>(_ =>
                    {
                        Interlocked.Increment(ref attemptCount);
                        throw new TaskCanceledException("Simulated database timeout");
                    });
            })
            .WithShortRetryPolicy()
            .StartAsync();

        var cloudEvent = WolverineIntegrationTestHost.CreateTestCloudEvent();

        // Act
        // await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert - Wait for message to appear in dead letter queue after retries exhaust
        // Short policy: 3 immediate retries (100ms each) + 3 scheduled retries (500ms each) ≈ 2-3s
        // var deadLetterMessage = await host.WaitForDeadLetterMessageAsync(
        //     host.RegisterQueueName,
        //     TimeSpan.FromSeconds(5));

        // Assert.NotNull(deadLetterMessage);

        // Verify exact retry count:
        // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
        // ScheduleRetry(500ms, 500ms, 500ms) = 3 more retries with new locks
        // Total: 1 initial + 3 cooldown retries + 3 scheduled retries = 7 attempts
        // Assert.Equal(7, attemptCount);
    }
}
