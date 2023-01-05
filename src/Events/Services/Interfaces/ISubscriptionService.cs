using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for subscription service
    /// </summary>
    public interface ISubscriptionService
    {
        /// <summary>
        /// Operation to create a subscription for an Altinn App event source
        /// </summary>
        /// <param name="eventsSubscription">The event subscription</param>
        /// <returns>A subscription if creation was successful or an errorr object</returns>
        public Task<(Subscription Subscription, ServiceError Error)> CreateAppSubscription(Subscription eventsSubscription);

        /// <summary>
        /// Operation to create a subscription for a generic event source
        /// </summary>
        /// <param name="eventsSubscription">The event subscription</param>
        /// <returns>A subscription if creation was successful or an errorr object</returns>
        public Task<(Subscription Subscription, ServiceError Error)> CreateGenericSubscription(Subscription eventsSubscription);

        /// <summary>
        /// Operation to delete a given subscriptions
        /// </summary>
        public Task<ServiceError> DeleteSubscription(int id);

        /// <summary>
        /// Update validation status
        /// </summary>
        public Task<(Subscription Subscription, ServiceError Error)> SetValidSubscription(int id);

        /// <summary>
        /// Get a given subscription
        /// </summary>
        /// <param name="id">The subcription Id</param>
        public Task<(Subscription Subscription, ServiceError Error)> GetSubscription(int id);

        /// <summary>
        /// Retrieves all subscriptions for the given consumer
        /// </summary>
        /// <param name="consumer">The subscription consumer</param>
        /// <returns>A list of subscriptions</returns>
        public Task<(List<Subscription> Subscription, ServiceError Error)> GetAllSubscriptions(string consumer);
    }
}
