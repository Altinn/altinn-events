#nullable enable
using System;
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
    /// Message should flow: RegisterQueue -> SaveEventHandler -> Save to DB -> InboundQueue
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
        await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert
        var receivedMessage = await host.WaitForMessageAsync(host.InboundQueueName);

        Assert.NotNull(receivedMessage);
        host.CloudEventRepositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests the retry policy when the database throws TaskCanceledException.
    /// Message should retry according to policy then move to dead letter queue.
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseThrowsTaskCanceledException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        var attemptCount = 0;

        await using var host = await new WolverineIntegrationTestHost(_fixture)
            .WithCleanQueues()
            .WithCloudEventRepository(mock =>
            {
                mock.Setup(r => r.CreateEvent(It.IsAny<string>()))
                    .Callback<string>(_ =>
                    {
                        attemptCount++;
                        throw new TaskCanceledException("Simulated database timeout");
                    });
            })
            .WithShortRetryPolicy()
            .StartAsync();

        var cloudEvent = WolverineIntegrationTestHost.CreateTestCloudEvent();

        // Act
        await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert - Wait for message to appear in dead letter queue after retries exhaust
        // Short policy: 3 immediate retries (100ms each) + 3 scheduled retries (500ms each) ≈ 2-3s
        var deadLetterMessage = await host.WaitForDeadLetterMessageAsync(
            host.RegisterQueueName,
            TimeSpan.FromSeconds(15));

        Assert.NotNull(deadLetterMessage);

        // Verify retries happened:
        // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
        // ScheduleRetry(500ms, 500ms, 500ms) = 3 more retries with new locks
        // Total: 1 initial + 3 cooldown retries + 3 scheduled retries = 7 attempts
        Assert.True(attemptCount >= 4, $"Expected at least 4 attempts (initial + 3 immediate retries), but got {attemptCount}");
    }
}
