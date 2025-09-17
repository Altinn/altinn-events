using Altinn.Platform.Events.Common.Models;

namespace Altinn.Platform.Events.Functions.Services.Interfaces;

/// <summary>
/// Service for managing retry backoff strategies for failed message processing.
/// </summary>
public interface IRetryBackoffService
{
    /// <summary>
    /// Requeues a failed message with appropriate backoff delay.
    /// </summary>
    /// <param name="message">The message to requeue.</param>
    /// <param name="exception">The exception that caused processing failure.</param>
    /// <returns>Task representing the requeue operation.</returns>
    Task RequeueWithBackoff(RetryableEventWrapper message, Exception exception);
}
