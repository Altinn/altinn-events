using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests.TestingServices;

public class RetryBackoffServiceTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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
            DequeueCount = 2,
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
        Assert.Equal(3, requeuedEvent.DequeueCount); // Incremented from 2 to 3
        Assert.Equal("test-correlation", requeuedEvent.CorrelationId);
        Assert.Equal("test-payload", requeuedEvent.Payload);
        Assert.Equal(TimeSpan.FromMinutes(1), capturedVisibility); // For dequeue count 3
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
            FirstProcessedAt = DateTime.UtcNow,
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
}
