using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Services
{
    /// <inheritdoc/>
    public class GenericSubscriptionService : SubscriptionService, IGenericSubscriptionService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        public GenericSubscriptionService(
            ISubscriptionRepository repository,
            IEventsQueueClient queue,
            IClaimsPrincipalProvider claimsPrincipalProvider,
            IProfile profile,
            IAuthorization authorization,
            IRegisterService register)
            : base(
                  repository,
                  queue,
                  claimsPrincipalProvider,
                  profile,
                  authorization,
                  register)
        {
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
        {
            await SetCreatedBy(eventsSubscription);

            if (string.IsNullOrEmpty(eventsSubscription.Consumer))
            {
                await EnrichConsumer(eventsSubscription);
            }

            if (!ValidateSubscription(eventsSubscription, out string message))
            {
                return (null, new ServiceError(400, message));
            }

            if (!AuthorizeSubscription(eventsSubscription))
            {
                var errorMessage = $"Not authorized to create a subscription with source {eventsSubscription.SourceFilter} and/ subject filter: {eventsSubscription.SubjectFilter}.";
                return (null, new ServiceError(401, errorMessage));
            }

            return await CompleteSubscriptionCreation(eventsSubscription);
        }

        private bool ValidateSubscription(Subscription eventsSubscription, out string message)
        {
            // what requirements do we have for a subscription to be valid? 
            // do we allow alternative subject for generic event subscriptions?
            message = null;
            return true;
        }

        private static bool AuthorizeSubscription(Subscription eventsSubscription)
        {
            // if consumer can be set at random, should and can we control who creates a subscription with a given consumer? consumer: "The queen", createdBy: /org/ttd or /user/123

            // Further authorization to be implemented in Altinn/altinn-events#259
            return true;
        }
    }
}
