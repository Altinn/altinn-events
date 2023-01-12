using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for methods related specifically to handling subscriptions for Altinn App event sources
    /// </summary>
    public interface IAppSubscriptionService
    {
        /// <summary>
        /// Operation to create a subscription for an Altinn App event source
        /// </summary>
        /// <param name="eventsSubscription">The event subscription</param>
        /// <returns>A subscription if creation was successful or an errorr object</returns>
        public Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription);
    }
}
