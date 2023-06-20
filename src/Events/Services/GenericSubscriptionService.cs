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
        private readonly IAuthorization _authorization;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        public GenericSubscriptionService(
            ISubscriptionRepository repository,
            IRegisterService register,
            IAuthorization authorization,
            IEventsQueueClient queue,
            IClaimsPrincipalProvider claimsPrincipalProvider)

            : base(
                  repository,
                  register,
                  queue,
                  claimsPrincipalProvider)
        {
            _authorization = authorization;
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

            if (!await _authorization.AuthorizeConsumerForEventsSubcription(eventsSubscription))
            {
                var errorMessage = $"Not authorized to create a subscription for resource {eventsSubscription.ResourceFilter} and/ subject filter: {eventsSubscription.SubjectFilter}.";
                return (null, new ServiceError(401, errorMessage));
            }

            return await CompleteSubscriptionCreation(eventsSubscription);
        }

        private static bool ValidateSubscription(Subscription eventsSubscription, out string message)
        {
            if (string.IsNullOrEmpty(eventsSubscription.ResourceFilter))
            {
                message = "Resource filter is required.";
                return false;
            }

            if (eventsSubscription.SourceFilter != null)
            {
                message = "Source filter is not supported for subscriptions on this resource.";
                return false;
            }

            if (!string.IsNullOrEmpty(eventsSubscription.AlternativeSubjectFilter))
            {
                message = "AlternativeSubject is not supported for subscriptions on this resource.";
                return false;
            }

            message = null;
            return true;
        }
    }
}
