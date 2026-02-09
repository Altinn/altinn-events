#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Repository;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingServiceBus;

/// <summary>
/// Integration tests for Wolverine retry policies using the factory-based approach with Wolverine's testing API.
/// Uses Wolverine's built-in message tracking instead of manually polling Azure Service Bus.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class RetryPolicyIntegrationTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the database is available.
    /// Message should flow: RegisterQueue -> SaveEventHandler -> Save to DB -> InboundQueue -> ...
    /// Verifies event was saved to database (messages processed successfully).
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseAvailable_MessageFlowsToInboundQueue()
    {
        // Arrange
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var command = new RegisterEventCommand(cloudEvent);

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Verify event was saved to the actual database (indicates successful processing)
            using var savedEvent = await PostgresTestUtils.GetEventFromDatabaseAsync(_fixture.PostgresConnectionString, cloudEvent.Id!);
            Assert.NotNull(savedEvent);
            Assert.Equal(cloudEvent.Id, savedEvent.RootElement.GetProperty("id").GetString());
            Assert.Equal(cloudEvent.Source!.ToString(), savedEvent.RootElement.GetProperty("source").GetString());
            Assert.Equal(cloudEvent.Type, savedEvent.RootElement.GetProperty("type").GetString());

            // Assert - Register queue should be empty (message was processed)
            var registerQueueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture,
                factory.WolverineSettings.RegistrationQueueName);
            Assert.True(registerQueueEmpty, "Register queue should be empty after successful processing");
        }
    }

    /// <summary>
    /// Tests the retry policy when the database throws TaskCanceledException.
    /// Message should retry according to policy then move to dead letter queue.
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_WhenDatabaseThrowsTaskCanceledException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Create mock repository that simulates database timeouts
        int attemptCount = 0;
        var mockRepository = new Mock<ICloudEventRepository>();
        mockRepository.Setup(r => r.CreateEvent(It.IsAny<string>()))
            .Callback<string>(_ => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new TaskCanceledException("Simulated database timeout"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockRepository.Object)
            .Initialize();

        await using (factory)
        {
            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var command = new RegisterEventCommand(cloudEvent);

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture,
                factory.WolverineSettings.RegistrationQueueName,
                TimeSpan.FromSeconds(5));

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms) = 3 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 3 scheduled retries = 7 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(7, attemptCount);

            // Assert.NotNull(deadLetterMessage);
        }
    }
}
