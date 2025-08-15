using Altinn.Platform.Events.IsolatedFunctions.Models;

namespace Altinn.Platform.Events.IsolatedFunctions.Services
{
    public interface IRetryBackoffService
    {
        TimeSpan GetVisibilityTimeout(int deQueueCount);
        Task RequeueWithBackoff(RetryableEventWrapper message, Exception exception);
        Task SendToPoisonAsync(RetryableEventWrapper message);
    }
}
