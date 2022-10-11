using System.Threading.Tasks;
using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Clients.Interfaces
{
    /// <summary>
    /// Describes the necessary methods for an implementation of an events queue client.
    /// </summary>
    public interface IEventsQueueClient
    {
        /// <summary>
        /// Pushes the provided content to the inbound queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> PostInbound(string content);

        /// <summary>
        /// Pushes the provided content to the outbound queue
        /// </summary>
        /// <param name="content">The content to push to the queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> PostOutbound(string content);

        /// <summary>
        /// Pushes the provided content to the validation queue
        /// </summary>
        /// <param name="content">The content to push to the validation queue in string format</param>
        /// <returns>Returns a queue receipt</returns>
        public Task<QueuePostReceipt> PostSubscriptionValidation(string content);
    }
}
