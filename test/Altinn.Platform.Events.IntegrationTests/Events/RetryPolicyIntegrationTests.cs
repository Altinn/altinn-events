#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Events;

/// <summary>
/// Integration tests for Wolverine retry policies with integration test containers.
/// These tests verify that the retry policy correctly handles exceptions and moves messages to the error queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class RetryPolicyIntegrationTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the database is available.
    /// Message should flow: RegisterQueue -> SaveEventHandler -> Save to DB -> InboundQueue -> ...
    /// Verifies:
    /// - Register queue is empty (message was processed)
    /// - Register DLQ is empty (no failures)
    /// - Event was actually saved to the PostgreSQL database
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseAvailable_MessageFlowsToInboundQueue()
    {
        // Arrange
        await using var host = await new IntegrationTestHost(_fixture)
            .WithCleanQueues()
            .WithRealDatabase()
            .StartAsync();

        var cloudEvent = IntegrationTestHost.CreateTestCloudEvent();

        // Act
        await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert - Register queue should be empty (message was processed)
        var registerQueueEmpty = await host.WaitForEmptyAsync(host.RegisterQueueName);
        Assert.True(registerQueueEmpty, "Register queue should be empty after successful processing");

        // Assert - Verify event was saved to the actual database
        var savedEvent = await GetEventFromDatabaseAsync(host.PostgresConnectionString, cloudEvent.Id!);
        Assert.NotNull(savedEvent);
        Assert.Equal(cloudEvent.Id, savedEvent.RootElement.GetProperty("id").GetString());
        Assert.Equal(cloudEvent.Source!.ToString(), savedEvent.RootElement.GetProperty("source").GetString());
        Assert.Equal(cloudEvent.Type, savedEvent.RootElement.GetProperty("type").GetString());

        // Assert - Register DLQ should be empty (no failures)
        var registerDlqEmpty = await host.WaitForDeadLetterEmptyAsync(host.RegisterQueueName);
        Assert.True(registerDlqEmpty, "Register dead letter queue should be empty (no failures)");
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

        await using var host = await new IntegrationTestHost(_fixture)
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

        var cloudEvent = IntegrationTestHost.CreateTestCloudEvent();

        // Act
        await host.PublishAsync(new RegisterEventCommand(cloudEvent));

        // Assert - Wait for message to appear in dead letter queue after retries exhaust
        // Short policy: 3 immediate retries (100ms each) + 3 scheduled retries (500ms each) ≈ 2-3s
        var deadLetterMessage = await host.WaitForDeadLetterMessageAsync(
            host.RegisterQueueName,
            TimeSpan.FromSeconds(5));

        Assert.NotNull(deadLetterMessage);

        // Verify exact retry count:
        // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
        // ScheduleRetry(500ms, 500ms, 500ms) = 3 more retries with new locks
        // Total: 1 initial + 3 cooldown retries + 3 scheduled retries = 7 attempts
        Assert.Equal(7, attemptCount);
    }

    private static async Task<JsonDocument?> GetEventFromDatabaseAsync(string connectionString, string eventId)
    {
        // Retry a few times to allow for transaction commit and visibility
        const int maxAttempts = 10;
        const int delayMs = 100;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var command = dataSource.CreateCommand(
                "SELECT cloudevent FROM events.events WHERE cloudevent->>'id' = $1");
            command.Parameters.AddWithValue(eventId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                return JsonDocument.Parse(json);
            }

            // If not found and not the last attempt, wait before retrying
            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayMs);
            }
        }

        return null;
    }
}
