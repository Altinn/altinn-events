using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Models;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to Events Inbound queue API
    /// </summary>
    public interface IEventsClient
    {
        /// <summary>
        /// Stores a cloud event document to the events database.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to be stored</param>
        /// <returns>CloudEvent as stored in the database</returns>
        Task SaveCloudEvent(CloudEvent cloudEvent);

        /// <summary>
        /// Send cloudEvent for outbound processing.
        /// </summary>
        /// <param name="item">CloudEvent to send</param>
        Task PostInbound(CloudEvent item);

        /// <summary>
        /// Send cloudEvent for outbound processing.
        /// </summary>
        /// <param name="item">CloudEvent to send</param>
        Task PostOutbound(CloudEvent item);

        /// <summary>
        /// Validates a subscription
        /// </summary>
        /// <returns></returns>
        public Task ValidateSubscription(int subscriptionId);
    }
}
