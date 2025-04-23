using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// This interface describes the public contract of a repository implementation for <see cref="Subscription"/>.
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Attempt to find existing subscriptions with properties matching the given subscription.
    /// </summary>
    /// <param name="eventsSubscription">The subscription to be used as base in the search.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
    /// </param>
    Task<Subscription> FindSubscription(Subscription eventsSubscription, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an subscription in repository
    /// </summary>
    /// <param name="eventsSubscription">The subscription to persist in repository</param>
    Task<Subscription> CreateSubscription(Subscription eventsSubscription);

    /// <summary>
    /// Gets a specific subscription
    /// </summary>
    Task<Subscription> GetSubscription(int id);

    /// <summary>
    /// Deletes a given subscription
    /// </summary>
    Task DeleteSubscription(int id);

    /// <summary>
    /// Set a subscription as valid
    /// </summary>
    Task SetValidSubscription(int id);

    /// <summary>
    /// Retrieve subscriptions that have filters that match the given parameters.
    /// </summary>
    /// <param name="resource">Resource filter</param>
    /// <param name="subject">Subject filter</param>
    /// <param name="eventType">Event type filter</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
    /// </param>
    /// <returns>A task representing the asynchronous operation with a list of subscriptions.</returns>
    Task<List<Subscription>> GetSubscriptions(
        string resource, string subject, string eventType, CancellationToken cancellationToken);

    /// <summary>
    /// Gets subscriptions for a given consumer consumer = "/org/%" will return subscriptions for all orgs.
    /// </summary>
    Task<List<Subscription>> GetSubscriptionsByConsumer(string consumer, bool includeInvalid);
}
