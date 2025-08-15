using Altinn.Platform.Events.IsolatedFunctions.Models;

namespace Altinn.Platform.Events.IsolatedFunctions.Services
{
    /// <summary>
    /// Defines a service for managing exponential retry backoff strategies and handling messages that fail processing.
    /// </summary>
    /// <remarks>This interface provides methods for calculating visibility timeouts, requeuing messages with
    /// backoff delays, and sending messages to a poison queue. It is designed to support robust retry mechanisms and
    /// ensure failed messages are handled appropriately to maintain system stability.</remarks>
    public interface IRetryBackoffService
    {
        /// <summary>
        /// Calculates the visibility timeout for a message based on the number of times it has been dequeued.
        /// </summary>
        /// <param name="dequeueCount">The number of times the message has been dequeued. Must be a non-negative integer.</param>
        /// <returns>A <see cref="TimeSpan"/> representing the visibility timeout duration. The value may vary depending on the
        /// provided <paramref name="dequeueCount"/>.</returns>
        TimeSpan GetVisibilityTimeout(int dequeueCount);

        /// <summary>
        /// Requeues a message with a backoff delay based on the provided exception. The original exception is not re-thrown, the message is simply requeued
        /// </summary>
        /// <remarks>The backoff delay is determined by the nature of the exception and may vary depending
        /// on the implementation. This method ensures that failed messages are retried in a controlled manner to
        /// prevent overwhelming the system.</remarks>
        /// <param name="message">The message to be requeued, wrapped in a <see cref="RetryableEventWrapper"/> object.</param>
        /// <param name="exception">The exception that caused the message to fail, used to determine if the message should go directly to the poison queue.</param>
        /// <returns>A task that represents the asynchronous operation of requeuing the message.</returns>
        Task RequeueWithBackoff(RetryableEventWrapper message, Exception exception);

        /// <summary>
        /// Sends the specified message to the poison queue for further inspection or handling.
        /// </summary>
        /// <param name="message">The message to be sent to the poison queue. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SendToPoisonAsync(RetryableEventWrapper message);
    }
}
