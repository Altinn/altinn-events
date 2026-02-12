#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
using CloudNative.CloudEvents;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingServiceBus;

/// <summary>
/// Integration tests for Wolverine retry policies on the inbound queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class InboundQueueRetryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the normal flow where a subscription exists and authorization succeeds.
    /// InboundEventCommand -> SendToOutboundHandler -> queries subscriptions -> authorizes -> outbound -> webhook.
    /// </summary>
    [Fact]
    public async Task InboundEventCommand_WhenSubscriptionExists_EventDeliveredToWebhook()
    {
        // Arrange - Mock auth to authorize all consumers
        var authMock = new Mock<IAuthorization>();
        authMock.Setup(a => a.AuthorizeMultipleConsumersForAltinnAppEvent(
                It.IsAny<CloudEvent>(), It.IsAny<List<string>>()))
            .ReturnsAsync((CloudEvent _, List<string> consumers) =>
                consumers.ToDictionary(c => c, _ => true));
        authMock.Setup(a => a.AuthorizeMultipleConsumersForGenericEvent(
                It.IsAny<CloudEvent>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CloudEvent _, List<string> consumers, CancellationToken _) =>
                consumers.ToDictionary(c => c, _ => true));

        // Mock webhook to capture outbound delivery
        var webhookCalls = new ConcurrentBag<CloudEventEnvelope>();
        var webhookMock = new Mock<IWebhookService>();
        webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((e, _) => webhookCalls.Add(e))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => authMock.Object)
            .ReplaceService(_ => webhookMock.Object)
            .Initialize();

        await using (factory)
        {
            // Create a validated subscription in DB so OutboundService finds it
            var subscription = await SubscriptionTestData.CreateTestSubscriptionInDb(factory);
            await SubscriptionTestData.ValidateSubscription(factory, subscription.Id);

            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var command = new InboundEventCommand(cloudEvent.Serialize());

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for webhook to be called with the event
            var delivered = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(webhookCalls.Count > 0),
                maxAttempts: 30,
                delayMs: 500);
            Assert.True(delivered, "Event should be delivered to webhook via outbound queue");

            // Assert - Verify delivered event matches
            Assert.Contains(webhookCalls, e => e.CloudEvent.Id == cloudEvent.Id);
        }
    }

    /// <summary>
    /// Tests the retry policy when the outbound service throws.
    /// Message should retry according to policy then move to dead letter queue.
    /// </summary>
    [Fact]
    public async Task InboundEventCommand_WhenOutboundServiceThrows_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Mock outbound service to always fail
        int attemptCount = 0;
        var outboundMock = new Mock<IOutboundService>();
        outboundMock.Setup(o => o.PostOutbound(
                It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .Callback<CloudEvent, CancellationToken, bool>((_, _, _) => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new HttpRequestException("Simulated service unavailable"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => outboundMock.Object)
            .Initialize();

        await using (factory)
        {
            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            var command = new InboundEventCommand(cloudEvent.Serialize());

            // Act
            await factory.PublishMessageAsync(command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture,
                factory.WolverineSettings.InboundQueueName,
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
