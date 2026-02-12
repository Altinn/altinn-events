#nullable enable
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingServiceBus;

/// <summary>
/// Integration tests for Wolverine retry policies on the outbound queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class OutboundQueueRetryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where the webhook endpoint is available.
    /// OutboundEventCommand -> SendEventToSubscriberHandler -> webhook called.
    /// </summary>
    [Fact]
    public async Task OutboundEventCommand_WhenWebhookAvailable_EventDelivered()
    {
        // Arrange - Mock webhook to capture calls
        var webhookCalls = new ConcurrentBag<CloudEventEnvelope>();
        var webhookMock = new Mock<IWebhookService>();
        webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((e, _) => webhookCalls.Add(e))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => webhookMock.Object)
            .Initialize();

        await using (factory)
        {
            var envelope = CloudEventTestData.CreateTestEnvelope();
            var command = new OutboundEventCommand(envelope.Serialize());

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for webhook to be called
            var delivered = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(webhookCalls.Count > 0),
                maxAttempts: 30,
                delayMs: 500);
            Assert.True(delivered, "Event should be delivered to webhook");

            // Assert - Verify delivered event matches
            Assert.Contains(webhookCalls, e => e.CloudEvent.Id == envelope.CloudEvent.Id);
        }
    }

    /// <summary>
    /// Tests the retry policy when the webhook throws HttpRequestException.
    /// Message should retry according to outbound policy then move to dead letter queue.
    /// Outbound has more retries than other queues.
    /// </summary>
    [Fact]
    public async Task OutboundEventCommand_WhenWebhookThrows_RetriesAndMovesToDeadLetterQueue()
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
            var envelope = CloudEventTestData.CreateTestEnvelope();
            var command = new OutboundEventCommand(envelope.Serialize());

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture,
                factory.WolverineSettings.OutboundQueueName,
                TimeSpan.FromSeconds(10));
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms) = 1 retry within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms, 200ms, 200ms, 200ms, 200ms, 200ms) = 10 more retries
            // Total: 1 initial + 1 cooldown retry + 10 scheduled retries = 12 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(12, attemptCount);
        }
    }
}
