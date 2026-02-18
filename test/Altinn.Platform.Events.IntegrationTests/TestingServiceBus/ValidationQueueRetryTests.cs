#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingServiceBus;

/// <summary>
/// Integration tests for Wolverine retry policies on the subscription validation queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class ValidationQueueRetryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the webhook is available.
    /// ValidateSubscriptionCommand -> ValidateSubscriptionHandler -> webhook called -> subscription validated in DB.
    /// </summary>
    [Fact]
    public async Task ValidateSubscriptionCommand_WhenWebhookAvailable_SubscriptionValidatedInDb()
    {
        // Arrange - Mock webhook to succeed
        var webhookMock = new Mock<IWebhookService>();
        webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => webhookMock.Object)
            .Initialize();

        await using (factory)
        {
            // Create subscription in DB first (ValidateSubscriptionHandler calls SetValidSubscription)
            var subscription = await SubscriptionTestData.CreateTestSubscriptionInDb(factory);
            var command = new ValidateSubscriptionCommand(subscription);

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for subscription to be marked as validated
            var validated = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var sub = await SubscriptionTestData.GetSubscriptionFromDb(factory, subscription.Id);
                    return sub?.Validated == true;
                },
                maxAttempts: 30,
                delayMs: 500);
            Assert.True(validated, "Subscription should be validated after successful webhook delivery");

            // Assert - Validation queue should be empty
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture,
                factory.WolverineSettings.ValidationQueueName);
            Assert.True(queueEmpty, "Validation queue should be empty after successful processing");
        }
    }

    /// <summary>
    /// Tests the retry policy when the webhook throws HttpRequestException.
    /// Message should retry according to policy then move to dead letter queue.
    /// </summary>
    [Fact]
    public async Task ValidateSubscriptionCommand_WhenWebhookThrows_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Mock webhook to always fail
        int attemptCount = 0;
        var webhookMock = new Mock<IWebhookService>();
        webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((_, _) => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new HttpRequestException("Simulated webhook unavailable"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => webhookMock.Object)
            .Initialize();

        await using (factory)
        {
            var subscription = await SubscriptionTestData.CreateTestSubscriptionInDb(factory);
            var command = new ValidateSubscriptionCommand(subscription);

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture,
                factory.WolverineSettings.ValidationQueueName,
                TimeSpan.FromSeconds(5));
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms) = 5 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 5 scheduled retries = 9 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(9, attemptCount);
        }
    }
}
