using Altinn.Platform.Events.IsolatedFunctions.Models;

namespace Altinn.Platform.Events.IsolatedFunctions.Services
{
    public interface IRetryBackoffService
    {
        TimeSpan GetVisibilityTimeout(int deQueueCount);
        Task RequeueWithBackoff(RetryableEventWrapper message, Exception exception, string queueName);
        Task SendToPoisonQueue(RetryableEventWrapper message, string queueName);
    }
}