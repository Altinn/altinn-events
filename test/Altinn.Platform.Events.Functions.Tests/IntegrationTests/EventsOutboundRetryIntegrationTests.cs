using System.Text.Json;

using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.IntegrationTests;

public class EventsOutboundRetryIntegrationTests
{
    private const string _connectionString = "UseDevelopmentStorage=true"; // Azurite must be running
    private const string _queueName = "outbound-it";                // name used when requeueing
    private const string _poisonQueueName = _queueName + "-poison";

    private const string _cloudEventJson = "{" +
         "\"id\":\"de1c9d5c-a7c2-4ad0-9d5b-2d2e3b3f1e11\"," +
         "\"source\":\"https://example.com/source/123\"," +
         "\"specversion\":\"1.0\"," +
         "\"type\":\"app.instance.created\"," +
         "\"subject\":\"/party/50012356\"," +
         "\"time\":\"2024-01-13T09:47:41.1680188Z\"" +
         "}";

    private static QueueClient CreateQueue(string name)
    {
        var options = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
        var qc = new QueueClient(_connectionString, name, options);
        qc.CreateIfNotExists();
        qc.ClearMessages();
        return qc;
    }

    [RequiresAzuriteFact]
    public async Task Outbound_Failure_Requeues_With_Increased_DequeueCount()
    {
        // Arrange
        QueueClient mainQueue = CreateQueue(_queueName);
        QueueClient poisonQueue = CreateQueue(_poisonQueueName);

        // Delegates bound to real queue clients
        QueueSendDelegate mainSender = async (msg, vis, ttl, ct) =>
        {
            await mainQueue.SendMessageAsync(msg, vis, ttl, ct);
        };
        PoisonQueueSendDelegate poisonSender = async (msg, vis, ttl, ct) =>
        {
            await poisonQueue.SendMessageAsync(msg, vis, ttl, ct);
        };

        var retryService = new TestableRetryBackoffService(
            NullLogger<TestableRetryBackoffService>.Instance,
            mainSender,
            poisonSender);

        // Mock webhook service to fail
        var webhook = new Mock<IWebhookService>();
        webhook.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>()))
               .ThrowsAsync(new InvalidOperationException("Simulated failure"));

        var sut = new EventsOutbound(
            webhook.Object,
            retryService);

        var envelope = new CloudEventEnvelope
        {
            Pushed = DateTime.UtcNow,
            Endpoint = new Uri("https://hooks.example.test/endpoint"),
            Consumer = "/org/demo",
            SubscriptionId = 42,
            CloudEvent = _cloudEventJson.DeserializeToCloudEvent()
        };

        var wrapper = new RetryableEventWrapper
        {
            Payload = envelope.Serialize(),
        };

        await mainQueue.ClearMessagesAsync(); // Ensure queue is empty before test

        // Act: run function (which will attempt webhook -> fail -> requeue)
        await sut.Run(wrapper.Serialize());

        // Assert: after visibility timeout (1s for first retry (TestableRetryBackoffService override) the message appears.
        RetryableEventWrapper requeued = null;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        bool received = false;

        while (!cts.IsCancellationRequested)
        {
            // Try receive (only visible messages returned)
            var msgs = await mainQueue.ReceiveMessagesAsync(1, TimeSpan.FromSeconds(1), cts.Token);
            if (msgs.Value.Length > 0)
            {
                var msg = msgs.Value[0];
                var raw = msg.Body.ToString(); // Body is already decoded by the SDK
                requeued = raw.DeserializeToRetryableEventWrapper();
                received = true;
                break;
            }

            // Sleep small interval
            await Task.Delay(750, cts.Token);
        }

        Assert.True(received, "Did not receive requeued message within expected time (ensure Azurite is running).");
        Assert.NotNull(requeued);
        Assert.Equal(1, requeued!.DequeueCount); // Incremented from 0 -> 1
        Assert.Equal(wrapper.CorrelationId, requeued.CorrelationId);
        Assert.Equal(wrapper.FirstProcessedAt, requeued.FirstProcessedAt); // Should preserve

        // ensure not sent to poison
        var poisonPeek = await poisonQueue.PeekMessagesAsync(1);
        Assert.Empty(poisonPeek.Value);
    }

    [RequiresAzuriteFact]
    public async Task PermanentFailure_Goes_Directly_To_Poison()
    {
        var main = CreateQueue(_queueName + "-perm");
        var poison = CreateQueue(_queueName + "-perm-poison");

        await poison.ClearMessagesAsync(); // Ensure poison queue is empty

        QueueSendDelegate mainSender = (msg, vis, ttl, ct) => main.SendMessageAsync(msg, vis, ttl, ct);
        PoisonQueueSendDelegate poisonSender = (msg, vis, ttl, ct) => poison.SendMessageAsync(msg, vis, ttl, ct);
        var sut = new TestableRetryBackoffService(NullLogger<TestableRetryBackoffService>.Instance, mainSender, poisonSender);

        var envelope = new CloudEventEnvelope
        {
            Pushed = DateTime.UtcNow,
            Endpoint = new Uri("https://hooks.example.test/endpoint"),
            Consumer = "/org/demo",
            SubscriptionId = 42,
            CloudEvent = _cloudEventJson.DeserializeToCloudEvent()
        };

        var wrapper = new RetryableEventWrapper
        {
            Payload = envelope.Serialize(),
        };

        // Act
        await sut.RequeueWithBackoff(wrapper, new JsonException("bad payload"));

        await Task.Delay(2000); // Allow time for poison queue processing

        Assert.Empty((await main.PeekMessagesAsync(1)).Value);
        var poisonMsgs = await poison.PeekMessagesAsync(1);
        Assert.Single(poisonMsgs.Value);
        var body = poisonMsgs.Value[0].Body.ToString(); // Already decoded
        var stored = body.DeserializeToRetryableEventWrapper();
        Assert.Equal(0, stored!.DequeueCount); // not incremented
    }

    [RequiresAzuriteFact]
    public async Task MaxRetry_Exceeded_Sent_To_Poison()
    {
        var main = CreateQueue(_queueName + "-max");
        var poison = CreateQueue(_queueName + "-max-poison");

        QueueSendDelegate mainSender = (msg, vis, ttl, ct) => main.SendMessageAsync(msg, vis, ttl, ct);
        PoisonQueueSendDelegate poisonSender = (msg, vis, ttl, ct) => poison.SendMessageAsync(msg, vis, ttl, ct);
        var svc = new TestableRetryBackoffService(NullLogger<TestableRetryBackoffService>.Instance, mainSender, poisonSender);

        var wrapper = new RetryableEventWrapper
        {
            Payload = "{}",
            DequeueCount = 12, // next => 13 > max (12)
        };

        await svc.RequeueWithBackoff(wrapper, new Exception("still failing"));

        Assert.Empty((await main.PeekMessagesAsync(1)).Value);
        Assert.Single((await poison.PeekMessagesAsync(1)).Value);
    }
}
