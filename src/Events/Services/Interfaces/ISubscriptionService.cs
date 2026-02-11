using System.Collections.Generic;
using System.Threading;
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
        /// <param name="id">The subscription Id</param>
        public Task<(Subscription Subscription, ServiceError Error)> GetSubscription(int id);

        /// <summary>
        /// Retrieves all subscriptions for the given consumer
        /// </summary>
        /// <returns>A list of subscriptions created by the current user.</returns>
        public Task<(List<Subscription> Subscription, ServiceError Error)> GetAllSubscriptions();

        /// <summary>
        /// Sends a validation event to the subscription endpoint and validates the response. This is used to validate the subscription when it is created or updated.
        /// </summary>
        /// <param name="subscription">The subscription to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SendAndValidate(Subscription subscription, CancellationToken cancellationToken);
    }
}
