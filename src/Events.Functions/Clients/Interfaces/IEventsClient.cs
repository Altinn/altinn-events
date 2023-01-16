using System.Threading.Tasks;
using CloudNative.CloudEvents;

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
        /// <param name="cloudEvent">CloudEvent to send</param>
        Task PostInbound(CloudEvent cloudEvent);

        /// <summary>
        /// Send cloudEvent for outbound processing.
        /// </summary>
        /// <param name="cloudEvent">CloudEvent to send</param>
        Task PostOutbound(CloudEvent cloudEvent);

        /// <summary>
        /// Set a subscription as valid
        /// </summary>
        public Task ValidateSubscription(int subscriptionId);
    }
}
