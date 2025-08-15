using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Services
{
    /// <summary>
    /// Provides functionality for handling retryable events with exponential backoff and poison queue handling.
    /// </summary>
    /// <remarks>This service is designed to manage the requeuing of events that fail processing, applying a
    /// backoff strategy based on the number of retries. If the maximum retry count is exceeded or a permanent failure
    /// is detected, the event is moved to a poison queue for further investigation.</remarks>
    public class RetryBackoffService : IRetryBackoffService
    {
        private readonly ILogger<RetryBackoffService> _logger;
        private readonly QueueSendDelegate _sendToQueue;
        private readonly PoisonQueueSendDelegate _sendToPoison;
        private readonly int _maxDequeueCount = 12;
        private readonly TimeSpan _ttl = TimeSpan.FromDays(7);

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public RetryBackoffService(
            ILogger<RetryBackoffService> logger,
            QueueSendDelegate sendToQueue,
            PoisonQueueSendDelegate sendToPoison)
        {
            _logger = logger;
            _sendToQueue = sendToQueue;
            _sendToPoison = sendToPoison;
        }

        public async Task RequeueWithBackoff(
            RetryableEventWrapper wrapper,
            Exception exception)
        {
            // Permanent failure?
            if (exception is JsonException or ArgumentException)
            {
                _logger.LogWarning(exception,
                    "Permanent failure, sending to poison queue. CorrelationId={CorrelationId}",
                    wrapper.CorrelationId);
                await SendToPoisonAsync(wrapper);
                return;
            }

            var updated = wrapper with { DequeueCount = wrapper.DequeueCount + 1 };

            if (updated.DequeueCount > _maxDequeueCount)
            {
                _logger.LogWarning("Exceeded max retries, moving to poison queue. CorrelationId={CorrelationId}",
                    updated.CorrelationId);
                await SendToPoisonAsync(updated);
                return;
            }

            string payload = JsonSerializer.Serialize(updated, _jsonOptions);
            TimeSpan visibility = GetVisibilityTimeout(updated.DequeueCount);

            _logger.LogDebug(
                "Requeueing message CorrelationId={CorrelationId} DequeueCount={DequeueCount} Visibility={Visibility}",
                updated.CorrelationId,
                updated.DequeueCount,
                visibility);

            await _sendToQueue(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)), visibility, _ttl);
        }

        public async Task SendToPoisonAsync(RetryableEventWrapper wrapper)
        {
            string payload = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _sendToPoison(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)), TimeSpan.FromSeconds(0), _ttl);
        }

        public virtual TimeSpan GetVisibilityTimeout(int count) => count switch
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
            10 or 11 or _ => TimeSpan.FromHours(12)
        };
    }
}
