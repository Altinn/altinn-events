#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Wolverine.Commands;
using CloudNative.CloudEvents;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingServiceBus;

/// <summary>
/// Integration tests for Wolverine retry policies using the factory-based approach with Wolverine's testing API.
/// Uses Wolverine's built-in message tracking instead of manually polling Azure Service Bus.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class RegistrationQueueRetryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the database is available.
    /// Message should flow: RegisterQueue -> RegistrationEventHandler -> Save to DB -> InboundQueue -> ...
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
            var command = new RegisterEventCommand(cloudEvent.Serialize(), Guid.NewGuid().ToString());

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
        mockRepository.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, _) => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new TaskCanceledException("Simulated database timeout"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockRepository.Object)
            .Initialize();

        await using (factory)
        {
            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var command = new RegisterEventCommand(cloudEvent.Serialize(), null);

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture,
                factory.WolverineSettings.RegistrationQueueName,
                TimeSpan.FromSeconds(5));
            
            // Assert - Message should be in dead letter queue after retries are exhausted
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms) = 5 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 5 scheduled retries = 9 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(9, attemptCount);
        }
    }

    /// <summary>
    /// Tests that when the same idempotency id is submitted twice, the second registration is detected as a
    /// duplicate (via the database unique constraint), the event is saved only once, and the outbound service
    /// is never invoked for the duplicate.
    /// </summary>
    [Fact]
    public async Task RegisterEventCommand_DuplicateIdempotencyId_DoesNotInvokeOutboundServiceForDuplicate()
    {
        // Arrange
        var outboundServiceMock = new Mock<IOutboundService>();
        outboundServiceMock
            .Setup(s => s.PostOutbound(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => outboundServiceMock.Object)
            .Initialize();

        await using (factory)
        {
            var idempotencyId = Guid.NewGuid().ToString();

            var firstCloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var firstCommand = new RegisterEventCommand(firstCloudEvent.Serialize(), idempotencyId);

            var secondCloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var secondCommand = new RegisterEventCommand(secondCloudEvent.Serialize(), idempotencyId);

            // Act - publish the first message and let it flow all the way to the outbound service
            await factory.PublishMessageAsync(firstCommand);

            using var firstSavedEvent = await PostgresTestUtils.GetEventFromDatabaseAsync(_fixture.PostgresConnectionString, firstCloudEvent.Id!);
            Assert.NotNull(firstSavedEvent);

            // Act - publish a second, different cloud event but with the same idempotency id
            await factory.PublishMessageAsync(secondCommand);

            // Assert - register queue should be empty (second message was processed, i.e. not stuck/retried)
            var registerQueueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture,
                factory.WolverineSettings.RegistrationQueueName);
            Assert.True(registerQueueEmpty, "Register queue should be empty after processing the duplicate");

            // Assert - the duplicate event was never saved to the database
            using var secondSavedEvent = await PostgresTestUtils.GetEventFromDatabaseAsync(
                _fixture.PostgresConnectionString,
                secondCloudEvent.Id!,
                maxAttempts: 3,
                delayMs: 200);
            Assert.Null(secondSavedEvent);

            // Assert - outbound service was invoked exactly once (only for the first, non-duplicate event)
            outboundServiceMock.Verify(
                s => s.PostOutbound(It.Is<CloudEvent>(c => c.Id == firstCloudEvent.Id), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
                Times.Once);
            outboundServiceMock.Verify(
                s => s.PostOutbound(It.Is<CloudEvent>(c => c.Id == secondCloudEvent.Id), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
                Times.Never);
        }
    }
}
