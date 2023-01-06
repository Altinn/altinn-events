using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platorm.Events.Extensions;

namespace Altinn.Platform.Events.Services
{
    /// <inheritdoc/>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _repository;
        private readonly IEventsQueueClient _queue;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
        private readonly IRegisterService _register;

        private const string UserPrefix = "/user/";
        private const string OrgPrefix = "/org/";
        private const string PartyPrefix = "/party/";

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        public SubscriptionService(ISubscriptionRepository repository, IEventsQueueClient queue, IClaimsPrincipalProvider claimsPrincipalProvider, IRegisterService register)
        {
            _repository = repository;
            _queue = queue;
            _claimsPrincipalProvider = claimsPrincipalProvider;
            _register = register;
        }

        /// <summary>
        /// Completes the common tasks related to creating a subcription once the producer specific services are completed
        /// </summary>       
        internal async Task<(Subscription Subscription, ServiceError Error)> CompleteSubscriptionCreation(Subscription eventsSubscription)
        {
            Subscription subscription = await _repository.FindSubscription(eventsSubscription, CancellationToken.None);

            subscription ??= await _repository.CreateSubscription(eventsSubscription, eventsSubscription.SourceFilter.GetMD5Hash());

            await _queue.EnqueueSubscriptionValidation(JsonSerializer.Serialize(subscription));
            return (subscription, null);
        }

        /// <inheritdoc/>
        public async Task<ServiceError> DeleteSubscription(int id)
        {
            (Subscription subscription, ServiceError error) = await GetSubscription(id);

            if (error != null)
            {
                return error;
            }

            if (!await AuthorizeAccessToSubscription(subscription))
            {
                error = new ServiceError(401);

                return error;
            }

            await _repository.DeleteSubscription(id);
            return null;
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> GetSubscription(int id)
        {
            var subscription = await _repository.GetSubscription(id);

            if (subscription == null)
            {
                return (null, new ServiceError(404));
            }

            if (!await AuthorizeAccessToSubscription(subscription))
            {
                return (null, new ServiceError(401));
            }

            return (subscription, null);
        }

        /// <inheritdoc/>
        public async Task<(List<Subscription> Subscription, ServiceError Error)> GetAllSubscriptions(string consumer)
        {
            var subscriptions = await _repository.GetSubscriptionsByConsumer(consumer, true);
            return (subscriptions, null);
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> SetValidSubscription(int id)
        {
            var subscription = await _repository.GetSubscription(id);

            if (subscription == null)
            {
                return (null, new ServiceError(404));
            }

            await _repository.SetValidSubscription(id);

            return (subscription, null);
        }

        /// <summary>
        /// Enriches the consumer based on the claims principal
        /// </summary>        
        internal async Task EnrichConsumer(Subscription eventsSubscription)
        {
            if (string.IsNullOrEmpty(eventsSubscription.Consumer))
            {
                var user = _claimsPrincipalProvider.GetUser();
                string org = user.GetOrg();
                if (!string.IsNullOrEmpty(org))
                {
                    eventsSubscription.Consumer = OrgPrefix + org;
                    return;
                }

                int? userId = user.GetUserIdAsInt();
                if (userId.HasValue)
                {
                    eventsSubscription.Consumer = UserPrefix + userId.Value;
                    return;
                }

                string organization = user.GetOrgNumber();
                if (!string.IsNullOrEmpty(organization))
                {
                    int partyId = await _register.PartyLookup(organization, null);
                    eventsSubscription.Consumer = PartyPrefix + partyId;
                }
            }
        }

        /// <summary>
        /// Sets created by on the subscription based on the claims principal
        /// </summary>
        internal async Task SetCreatedBy(Subscription eventsSubscription)
        {
            var user = _claimsPrincipalProvider.GetUser();

            string org = user.GetOrg();
            if (!string.IsNullOrEmpty(org))
            {
                eventsSubscription.CreatedBy = OrgPrefix + org;
                return;
            }

            int? userId = user.GetUserIdAsInt();
            if (userId.HasValue)
            {
                eventsSubscription.CreatedBy = UserPrefix + userId.Value;
                return;
            }

            string organization = user.GetOrgNumber();
            if (!string.IsNullOrEmpty(organization))
            {
                int partyId = await _register.PartyLookup(organization, null);
                eventsSubscription.CreatedBy = PartyPrefix + partyId;
            }
        }

        private async Task<bool> AuthorizeAccessToSubscription(Subscription eventsSubscription)
        {
            var user = _claimsPrincipalProvider.GetUser();
            string currentIdenity = string.Empty;

            if (!string.IsNullOrEmpty(user.GetOrg()))
            {
                currentIdenity = OrgPrefix + user.GetOrg();
            }
            else if (!string.IsNullOrEmpty(user.GetOrgNumber()))
            {
                currentIdenity = PartyPrefix + await _register.PartyLookup(user.GetOrgNumber(), null);
            }
            else if (user.GetUserIdAsInt().HasValue)
            {
                currentIdenity = UserPrefix + user.GetUserIdAsInt().Value;
            }

            if (eventsSubscription.CreatedBy.Equals(currentIdenity))
            {
                return true;
            }

            return false;
        }
    }
}
