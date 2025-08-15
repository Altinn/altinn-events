using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests.IntegrationTests;

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

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static QueueClient CreateQueue(string name)
    {
        var qc = new QueueClient(_connectionString, name);
        qc.CreateIfNotExists();
        // Clear any residual messages deterministically
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
        var webhook = new Moq.Mock<IWebhookService>();
        webhook.Setup(w => w.Send(Moq.It.IsAny<CloudEventEnvelope>()))
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
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        await mainQueue.ClearMessagesAsync(); // Ensure queue is empty before test

        // Act: run function (which will attempt webhook -> fail -> requeue)
        await sut.Run(wrapper.Serialize());

        // Assert: after visibility timeout (10s for first retry) the message appears.
        RetryableEventWrapper? requeued = null;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        var start = DateTime.UtcNow;
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
    public async Task Backoff_Progression_Sequence_Matches_Custom_Visibility()
    {
        var main = CreateQueue(_queueName + "-prog");
        var poison = CreateQueue(_queueName + "-prog-poison");

        QueueSendDelegate mainSender = (msg, vis, ttl, ct) => main.SendMessageAsync(msg, vis, ttl, ct);
        PoisonQueueSendDelegate poisonSender = (msg, vis, ttl, ct) => poison.SendMessageAsync(msg, vis, ttl, ct);

        var svc = new TestableRetryBackoffService(NullLogger<TestableRetryBackoffService>.Instance, mainSender, poisonSender);

        // We'll drive several failures manually (simulate the function’s catch flow)
        int[] plannedCounts = { 0, 1, 2, 3 }; // expect requeued as 1,2,3,4 with vis 1s,2s,3s,4s
        foreach (var current in plannedCounts)
        {
            await main.ClearMessagesAsync();
            var wrapper = new RetryableEventWrapper
            {
                Payload = "{}",
                DequeueCount = current,
                FirstProcessedAt = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Simulate transient failure requeue
            await svc.RequeueWithBackoff(wrapper, new Exception("transient"));

            // Immediately attempt receive -> should often be empty (still invisible)
            var immediate = await main.ReceiveMessagesAsync(1, TimeSpan.FromSeconds(1));
            Assert.Empty(immediate.Value);

            // Wait up to expectedVisibility + small slack
            var expectedVis = svc.GetVisibilityTimeout(current + 1);
            var deadline = DateTime.UtcNow + expectedVis.Add(TimeSpan.FromSeconds(1));
            RetryableEventWrapper? received = null;

            while (DateTime.UtcNow < deadline)
            {
                var msgs = await main.ReceiveMessagesAsync(1, TimeSpan.FromSeconds(1));
                if (msgs.Value.Length > 0)
                {
                    var raw = msgs.Value[0].Body.ToString(); // Already decoded
                    received = raw.DeserializeToRetryableEventWrapper();
                    break;
                }
                await Task.Delay(100);
            }

            Assert.NotNull(received);
            Assert.Equal(current + 1, received!.DequeueCount);
        }

        // Ensure poison unused
        Assert.Empty((await poison.PeekMessagesAsync(1)).Value);
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
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
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
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        await svc.RequeueWithBackoff(wrapper, new Exception("still failing"));

        Assert.Empty((await main.PeekMessagesAsync(1)).Value);
        Assert.Single((await poison.PeekMessagesAsync(1)).Value);
    }
}
