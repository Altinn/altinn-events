using Altinn.Platform.Events.IsolatedFunctions.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Services;

public class RetryBackoffService
{
    private readonly ILogger<RetryBackoffService> _logger;
    private readonly QueueClientFactory _queueClientFactory;
    private readonly int _maxDequeueCount = 12;
    private readonly TimeSpan _timeToLive = TimeSpan.FromDays(7);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RetryBackoffService(
        ILogger<RetryBackoffService> logger,
        QueueClientFactory queueClientFactory,
        string initialQueueType = "events")
    {
        _logger = logger;
        _queueClientFactory = queueClientFactory;
    }

    public async Task RequeueWithBackoff(RetryableEventWrapper message, Exception exception, string queueName)
    {
        // Get the appropriate queue client on demand
        QueueClient queueClient = _queueClientFactory.GetQueueClient(queueName);
        
        // Don't retry on certain exceptions
        if (exception is JsonException || exception is ArgumentException)
        {
            _logger.LogWarning("Permanent error detected - sending to poison queue immediately");
            await SendToPoisonQueue(message, queueName);
            return;
        }

        // For transient errors, apply backoff strategy
        var newMessage = message with
        {
            DequeueCount = message.DequeueCount + 1 // Increment the dequeue count
        };

        if (newMessage.DequeueCount > _maxDequeueCount)
        {
            // If the message has been dequeued too many times, send it to the poison queue
            _logger.LogWarning("Message has exceeded maximum dequeue count. Sending to poison queue.");
            await SendToPoisonQueue(newMessage, queueName);
            return;
        }
        else
        {
            // Serialize the metadata and original message
            string newMessageSerialized = JsonSerializer.Serialize(newMessage, _jsonOptions);

            await queueClient.SendMessageAsync(newMessageSerialized,
                GetVisibilityTimeout(newMessage.DequeueCount), // Visibility timeout (delay before processing)
                _timeToLive); // Time-to-live
        }
    }

    public async Task SendToPoisonQueue(RetryableEventWrapper message, string queueName)
    {
        // Get the poison queue client on demand
        QueueClient poisonQueueClient = _queueClientFactory.GetPoisonQueueClient(queueName);
        
        // Serialize the message for the poison queue
        string messageSerialized = JsonSerializer.Serialize(message, _jsonOptions);
        await poisonQueueClient.SendMessageAsync(messageSerialized, TimeSpan.FromSeconds(0), TimeSpan.FromDays(7));
    }

    public virtual TimeSpan GetVisibilityTimeout(int deQueueCount)
    {
        var visibilityTimeout = deQueueCount switch
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
            10 => TimeSpan.FromHours(12),
            11 => TimeSpan.FromHours(12),
            _ => TimeSpan.FromHours(12),
        };

        return visibilityTimeout;
    }
}
