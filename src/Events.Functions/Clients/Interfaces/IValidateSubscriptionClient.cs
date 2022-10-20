using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to Events Subscription validation API
    /// </summary>
    public interface IValidateSubscriptionClient
    {
        /// <summary>
        /// Validates a subscription
        /// </summary>
        /// <returns></returns>
        public Task ValidateSubscription(int subscriptionId);
    }
}
