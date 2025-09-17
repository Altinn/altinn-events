using System.Text.Json;

using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Functions.Services;

/// <summary>
/// Implementation of <see cref="IRetryBackoffService"/> that provides exponential backoff retries
/// and handles message failure with configurable retry limits.
/// </summary>
public class RetryBackoffService : IRetryBackoffService
{
    private readonly ILogger<RetryBackoffService> _logger;
    private readonly QueueSendDelegate _sendToQueue;
    private readonly PoisonQueueSendDelegate _sendToPoison;
    private readonly int _maxDequeueCount = 12;
    private readonly TimeSpan _ttl = TimeSpan.FromDays(7);

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryBackoffService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for diagnostic and operational messages.</param>
    /// <param name="sendToQueue">Delegate to send messages to the primary queue.</param>
    /// <param name="sendToPoison">Delegate to send messages to the poison queue.</param>
    public RetryBackoffService(
        ILogger<RetryBackoffService> logger,
        QueueSendDelegate sendToQueue,
        PoisonQueueSendDelegate sendToPoison)
    {
        _logger = logger;
        _sendToQueue = sendToQueue;
        _sendToPoison = sendToPoison;
    }

    /// <inheritdoc/>
    public async Task RequeueWithBackoff(
        RetryableEventWrapper message,
        Exception exception)
    {
        // Permanent failure?
        if (exception is JsonException or ArgumentException)
        {
            _logger.LogWarning(
                exception,
                "Permanent failure, sending to poison queue. CorrelationId={CorrelationId}",
                message.CorrelationId);
            await SendToPoisonAsync(message);
            return;
        }

        var updated = message with { DequeueCount = message.DequeueCount + 1 };

        if (updated.DequeueCount > _maxDequeueCount)
        {
            _logger.LogWarning(
                "Exceeded max retries, moving to poison queue. CorrelationId={CorrelationId}",
                updated.CorrelationId);
            await SendToPoisonAsync(updated);
            return;
        }

        string payload = updated.Serialize();
        TimeSpan visibility = GetVisibilityTimeout(updated.DequeueCount);

        _logger.LogDebug(
            "Requeueing message CorrelationId={CorrelationId} DequeueCount={DequeueCount} Visibility={Visibility}",
            updated.CorrelationId,
            updated.DequeueCount,
            visibility);

        await _sendToQueue(payload, visibility, _ttl);
    }

    /// <summary>
    /// Sends a message to the poison queue when it can no longer be processed.
    /// </summary>
    /// <param name="message">The message to send to the poison queue.</param>
    /// <returns>Task representing the send operation.</returns>
    private async Task SendToPoisonAsync(RetryableEventWrapper message)
    {
        string payload = message.Serialize();
        await _sendToPoison(payload, TimeSpan.FromSeconds(0), _ttl);
    }

    /// <summary>
    /// Calculates visibility timeout based on the message's dequeue count.
    /// </summary>
    /// <param name="dequeueCount">Number of times the message has been dequeued.</param>
    /// <returns>TimeSpan representing the appropriate visibility timeout.</returns>
    internal virtual TimeSpan GetVisibilityTimeout(int dequeueCount) => dequeueCount switch
    {
        1 => TimeSpan.FromSeconds(10),
        2 => TimeSpan.FromSeconds(30),
        3 => TimeSpan.FromMinutes(1),
        4 => TimeSpan.FromMinutes(5),
        5 => TimeSpan.FromMinutes(10),
        6 => TimeSpan.FromMinutes(30),
        7 => TimeSpan.FromHours(1),
        8 => TimeSpan.FromHours(3),
        9 => TimeSpan.FromHours(6),
        _ => TimeSpan.FromHours(12)
    };
}
