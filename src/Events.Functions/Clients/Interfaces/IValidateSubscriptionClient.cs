using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface for validate subscription
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
