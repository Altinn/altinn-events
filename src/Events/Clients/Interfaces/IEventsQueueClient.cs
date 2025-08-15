using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Clients.Interfaces
{
    /// <summary>
    /// Describes the necessary methods for an implementation of an events queue client.
    /// </summary>
    public interface IEventsQueueClient
    {
        /// <summary>
        /// Enqueues the provided content to the registration queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> EnqueueRegistration(string content);

        /// <summary>
        /// Enqueues the provided CloudEvent to the registration queue.
        /// Overload that accepts a CloudEvent object directly, allowing callers or implementations to perform any necessary wrapping/serialization.
        /// </summary>
        /// <param name="cloudEvent">The cloud event object that contains the payload <see cref="CloudEvent"/></param>
        /// <returns>Returns a queue receipt</returns>
        Task<QueuePostReceipt> EnqueueRegistration(CloudEvent cloudEvent);
        /// <summary>
        /// Enqueues the provided content to the inbound queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> EnqueueInbound(string content);

        /// <summary>
        /// Enqueues the provided content to the outbound queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> EnqueueOutbound(string content);

        /// <summary>
        /// Enqueues the provided content to the validation queue
        /// </summary>
        /// <param name="content">The content to push to the validation queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> EnqueueSubscriptionValidation(string content);
    }
}
