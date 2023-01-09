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
            IRegisterService register,
            IEventsQueueClient queue,
            IClaimsPrincipalProvider claimsPrincipalProvider)

            : base(
                  repository,
                  register,
                  queue,
                  claimsPrincipalProvider)
        {
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
        {
            await SetCreatedBy(eventsSubscription);

            if (!ValidateSubscription(eventsSubscription, out string message))
            {
                return (null, new ServiceError(400, message));
            }

            if (!AuthorizeSubscription())
            {
                var errorMessage = $"Not authorized to create a subscription with source {eventsSubscription.SourceFilter} and/ subject filter: {eventsSubscription.SubjectFilter}.";
                return (null, new ServiceError(401, errorMessage));
            }

            return await CompleteSubscriptionCreation(eventsSubscription);
        }

        private static bool ValidateSubscription(Subscription eventsSubscription, out string message)
        {
            if (string.IsNullOrEmpty(eventsSubscription.Consumer))
            {
                message = "Consumer is required.";
                return false;
            }

            if (!string.IsNullOrEmpty(eventsSubscription.AlternativeSubjectFilter))
            {
                message = "AlternativeSubject is not supported for subscriptions on generic event sources.";
                return false;
            }

            message = null;
            return true;
        }

        private static bool AuthorizeSubscription()
        {
            // if consumer can be set at random, should and can we control who creates a subscription with a given consumer? consumer: "The queen", createdBy: /org/ttd or /user/123

            // Further authorization to be implemented in Altinn/altinn-events#259
            return true;
        }
    }
}
