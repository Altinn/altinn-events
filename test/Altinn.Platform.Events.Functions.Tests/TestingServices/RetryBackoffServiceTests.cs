using System.Text.Json;

using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.Functions.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices;

public class RetryBackoffServiceTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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
    public async Task RequeueWithBackoff_TransientFailure_IncreasesDequeueCount()
    {
        // Arrange
        string capturedMessage = null;
        TimeSpan? capturedVisibility = null;
        var mainSenderMock = new Mock<QueueSendDelegate>();
        var poisonSenderMock = new Mock<PoisonQueueSendDelegate>();

        mainSenderMock
            .Setup(m => m(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((msg, vis, ttl, ct) =>
            {
                capturedMessage = msg;
                capturedVisibility = vis;
            })
            .Returns(Task.CompletedTask);

        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainSenderMock.Object,
            poisonSenderMock.Object);

        var originalEvent = new RetryableEventWrapper
        {
            Payload = "test-payload",
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            CorrelationId = "test-correlation"
        };

        // Act
        await sut.RequeueWithBackoff(originalEvent, new InvalidOperationException("Test transient failure"));

        // Assert
        mainSenderMock.Verify(
            m => m(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        poisonSenderMock.Verify(
            p => p(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var requeuedEvent = JsonSerializer.Deserialize<RetryableEventWrapper>(capturedMessage, _jsonOptions);
        Assert.Equal(1, requeuedEvent.DequeueCount); // Incremented from 1 to 2
        Assert.Equal("test-correlation", requeuedEvent.CorrelationId);
        Assert.Equal("test-payload", requeuedEvent.Payload);
        Assert.Equal(TimeSpan.FromSeconds(10), capturedVisibility); // For dequeue count 1
    }

    [Fact]
    public async Task RequeueWithBackoff_PermanentFailure_SendsDirectlyToPoisonQueue()
    {
        // Arrange
        string capturedPoisonMessage = null;

        var mainSenderMock = new Mock<QueueSendDelegate>();
        var poisonSenderMock = new Mock<PoisonQueueSendDelegate>();

        poisonSenderMock
            .Setup(p => p(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((msg, vis, ttl, ct) => capturedPoisonMessage = msg)
            .Returns(Task.CompletedTask);

        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainSenderMock.Object,
            poisonSenderMock.Object);

        var originalEvent = new RetryableEventWrapper
        {
            Payload = "invalid-json",
            DequeueCount = 0,
            CorrelationId = "test-correlation"
        };

        // Act - using JsonException which should be treated as permanent
        await sut.RequeueWithBackoff(originalEvent, new JsonException("Invalid JSON"));

        // Assert
        mainSenderMock.Verify(
            m => m(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        poisonSenderMock.Verify(
            p => p(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var poisonedEvent = JsonSerializer.Deserialize<RetryableEventWrapper>(capturedPoisonMessage, _jsonOptions);
        Assert.Equal(0, poisonedEvent.DequeueCount); // Not incremented
        Assert.Equal("test-correlation", poisonedEvent.CorrelationId);
        Assert.Equal("invalid-json", poisonedEvent.Payload);
    }

    [Fact]
    public async Task RequeueWithBackoff_MaxDequeueCountExceeded_SendsToPoisonQueue()
    {
        // Arrange
        string capturedPoisonMessage = null;

        var mainSenderMock = new Mock<QueueSendDelegate>();
        var poisonSenderMock = new Mock<PoisonQueueSendDelegate>();

        poisonSenderMock
            .Setup(p => p(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((msg, vis, ttl, ct) => capturedPoisonMessage = msg)
            .Returns(Task.CompletedTask);

        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainSenderMock.Object,
            poisonSenderMock.Object);

        var originalEvent = new RetryableEventWrapper
        {
            Payload = "max-retry-payload",
            DequeueCount = 12, // Assuming 12 is max
            FirstProcessedAt = DateTime.UtcNow.AddHours(-1),
            CorrelationId = "test-correlation"
        };

        // Act
        await sut.RequeueWithBackoff(originalEvent, new TimeoutException("Test timeout"));

        // Assert
        mainSenderMock.Verify(
            m => m(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        poisonSenderMock.Verify(
            p => p(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var poisonedEvent = JsonSerializer.Deserialize<RetryableEventWrapper>(capturedPoisonMessage, _jsonOptions);
        Assert.Equal(13, poisonedEvent.DequeueCount); // Still gets incremented
        Assert.Equal("test-correlation", poisonedEvent.CorrelationId);
    }

    [Theory]
    [InlineData(1, "00:00:10")] // 10 seconds
    [InlineData(2, "00:00:30")] // 30 seconds
    [InlineData(3, "00:01:00")] // 1 minute
    [InlineData(4, "00:05:00")] // 5 minutes
    [InlineData(5, "00:10:00")] // 10 minutes
    [InlineData(6, "00:30:00")] // 30 minutes
    [InlineData(7, "01:00:00")] // 1 hour
    [InlineData(8, "03:00:00")] // 3 hours
    [InlineData(9, "06:00:00")] // 6 hours
    [InlineData(10, "12:00:00")] // 12 hours
    [InlineData(11, "12:00:00")] // 12 hours
    [InlineData(12, "12:00:00")] // 12 hours
    public void GetVisibilityTimeout_ReturnsExpectedBackoffForDequeueCount(int dequeueCount, string expectedTimespan)
    {
        // Arrange
        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            (_, _, _, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask);

        // Act
        var result = sut.GetVisibilityTimeout(dequeueCount);

        // Assert
        Assert.Equal(TimeSpan.Parse(expectedTimespan), result);
    }

    [Fact]
    public async Task RequeueWithBackoff_PreservesEventMetadata()
    {
        // Arrange
        string capturedMessage = null;
        DateTime originalTimestamp = DateTime.UtcNow.AddMinutes(-10);
        string correlationId = Guid.NewGuid().ToString();

        var mainSenderMock = new Mock<QueueSendDelegate>();
        mainSenderMock
            .Setup(m => m(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((msg, vis, ttl, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var sut = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainSenderMock.Object,
            (_, _, _, _) => Task.CompletedTask);

        var originalEvent = new RetryableEventWrapper
        {
            Payload = "test-metadata-preservation",
            DequeueCount = 1,
            FirstProcessedAt = originalTimestamp,
            CorrelationId = correlationId
        };

        // Act
        await sut.RequeueWithBackoff(originalEvent, new Exception("Test failure"));

        // Assert
        var requeuedEvent = JsonSerializer.Deserialize<RetryableEventWrapper>(capturedMessage, _jsonOptions);
        Assert.Equal(correlationId, requeuedEvent.CorrelationId);
        Assert.Equal(originalTimestamp, requeuedEvent.FirstProcessedAt);
        Assert.Equal("test-metadata-preservation", requeuedEvent.Payload);
    }

    [Fact]
    public async Task RequeueWithBackoff_IncrementsDequeue_And_UsesVisibility()
    {
        // Arrange
        string sentMain = null;
        string sentPoison = null;
        TimeSpan? capturedVisibility = null;

        QueueSendDelegate mainDelegate = (msg, vis, ttl, ct) =>
        {
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
        string sentMain = null;
        string sentPoison = null;

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
        };

        await svc.RequeueWithBackoff(wrapper, new JsonException("Bad json"));

        Assert.Null(sentMain);
        Assert.NotNull(sentPoison);
    }
}
