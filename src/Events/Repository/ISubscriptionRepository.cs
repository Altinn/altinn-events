using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// This interface describes the public contract of a repository implementation for <see cref="Subscription"/>
    /// </summary>
    public interface ISubscriptionRepository
    {
        /// <summary>
        /// Attempt to find existing subscriptions with properties matching the given subscription.
        /// </summary>
        /// <param name="eventsSubscription">The subscription to be used as base in the search.</param>
        /// <param name="ct">A token to cancel the asynchronous operation if needed.</param>
        Task<Subscription> FindSubscription(Subscription eventsSubscription, CancellationToken ct);

        /// <summary>
        /// Creates an subscription in repository
        /// </summary>
        /// <param name="eventsSubscription">The subscription to persist in repository</param>
        /// <param name="sourceFilterHash">The hashed subscription source filter to persist in repository</param>
        Task<Subscription> CreateSubscription(Subscription eventsSubscription, string sourceFilterHash);

        /// <summary>
        /// Gets a specific subscription
        /// </summary>
        Task<Subscription> GetSubscription(int id);

        /// <summary>
        /// Deletes a given subscription
        /// </summary>
        Task DeleteSubscription(int id);

        /// <summary>
        /// Set a subscription as Valid
        /// </summary>
        Task SetValidSubscription(int id);

        /// <summary>
        /// Gets subscriptions by source, subject and type of a cloud event
        /// </summary>
        Task<List<Subscription>> GetSubscriptions(string source, string subject, string type, CancellationToken ct);

        /// <summary>
        /// Gets subscriptions for a given consumer
        /// consumer = "/org/%" will return subscriptions for all orgs
        /// </summary>
        Task<List<Subscription>> GetSubscriptionsByConsumer(string consumer, bool includeInvalid);
    }
}
