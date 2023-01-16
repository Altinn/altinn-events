using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
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
            var currentEntity = await GetEntityFromPrincipal();
            eventsSubscription.CreatedBy = currentEntity;
            eventsSubscription.Consumer = currentEntity;

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
            if (!UriExtensions.IsValidUrlOrUrn(eventsSubscription.SourceFilter))
            {
                message = "Source filter must be a valid URN or a URL using https scheme.";
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
            // Further authorization to be implemented in Altinn/altinn-events#259
            return true;
        }
    }
}
