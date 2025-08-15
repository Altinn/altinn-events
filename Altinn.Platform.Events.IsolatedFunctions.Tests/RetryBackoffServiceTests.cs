using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests;

public class RetryBackoffServiceTests
{
    private readonly string _serializedCloudEnvelope = "{" +
        "\"Pushed\": \"2023-01-17T16:09:10.9090958+00:00\"," +
        "  \"Endpoint\": \"https://hooks.slack.com/services/weebhook-endpoint\"," +
        "  \"Consumer\": \"/org/ttd\"," +
        "  \"SubscriptionId\": 427," +
        "  \"CloudEvent\": {" +
            " \"specversion\": \"1.0\"," +
            " \"id\": \"42849881-3659-4ff4-9ee1-c577646ea44b\"," +
            " \"source\": \"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d\"," +
            " \"type\": \"app.instance.created\"," +
            " \"subject\": \"/party/50002108\"," +
            " \"time\": \"2023-01-17T16:09:07.3146561Z\"," +
            " \"alternativesubject\": \"/person/01014922047\"}}";

    [Fact]
    public async Task RequeueWithBackoff_IncrementsDequeue_And_UsesVisibility()
    {
        // Arrange
        string? sentMain = null;
        string? sentPoison = null;
        TimeSpan? capturedVisibility = null;

        QueueSendDelegate mainDelegate = (msg, vis, ttl, ct) =>
        {
            // Decode base64 back to UTF8 string
            sentMain = msg;
            capturedVisibility = vis;
            return Task.CompletedTask;
        };

        PoisonQueueSendDelegate poisonDelegate = (msg, vis, ttl, ct) =>
        {
            sentPoison = msg;
            return Task.CompletedTask;
        };

        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainDelegate,
            poisonDelegate);

        var wrapper = new RetryableEventWrapper
        {
            Payload = _serializedCloudEnvelope,
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        await sut.RequeueWithBackoff(wrapper, new Exception("Transient"));

        // Assert
        Assert.Null(sentPoison);
        Assert.NotNull(sentMain);
        var updated = sentMain.DeserializeToRetryableEventWrapper();
        Assert.Equal(1, updated?.DequeueCount);
        Assert.Equal(TimeSpan.FromSeconds(10), capturedVisibility);
    }

    [Fact]
    public async Task RequeueWithBackoff_PermanentError_GoesToPoison()
    {
        string? sentMain = null;
        string? sentPoison = null;

        QueueSendDelegate mainDelegate = (msg, vis, ttl, ct) =>
        {
            sentMain = msg;
            return Task.CompletedTask;
        };
        PoisonQueueSendDelegate poisonDelegate = (msg, vis, ttl, ct) =>
        {
            sentPoison = msg;
            return Task.CompletedTask;
        };

        var svc = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainDelegate,
            poisonDelegate);

        var wrapper = new RetryableEventWrapper
        {
            Payload = _serializedCloudEnvelope,
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        await svc.RequeueWithBackoff(wrapper, new JsonException("Bad json"));

        Assert.Null(sentMain);
        Assert.NotNull(sentPoison);
    }
}
