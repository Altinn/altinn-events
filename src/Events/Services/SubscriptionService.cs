using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using Altinn.Platorm.Events.Extensions;

namespace Altinn.Platform.Events.Services
{
    /// <inheritdoc/>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _repository;
        private readonly IEventsQueueClient _queue;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
        private readonly IProfile _profile;
        private readonly IRegisterService _register;
        private readonly IAuthorization _authorization;

        private const string OrganisationPrefix = "/org/";
        private const string PersonPrefix = "/person/";
        private const string UserPrefix = "/user/";
        private const string OrgPrefix = "/org/";
        private const string PartyPrefix = "/party/";

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        public SubscriptionService(ISubscriptionRepository repository, IEventsQueueClient queue, IClaimsPrincipalProvider claimsPrincipalProvider, IProfile profile, IAuthorization authorization, IRegisterService register)
        {
            _repository = repository;
            _queue = queue;
            _claimsPrincipalProvider = claimsPrincipalProvider;
            _profile = profile;
            _authorization = authorization;
            _register = register;
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
        {
            await EnrichSubject(eventsSubscription);

            await SetCreatedBy(eventsSubscription);
            await EnrichConsumer(eventsSubscription);

            if (!ValidateSubscription(eventsSubscription, out string message))
            {
                return (null, new ServiceError(400, message));
            }

            if (!AuthorizeIdentityForConsumer(eventsSubscription))
            {
                var errorMessage = "Not authorized to create a subscription on behalf of " + eventsSubscription.Consumer;
                return (null, new ServiceError(401, errorMessage));
            }

            if (!await AuthorizeSubjectForConsumer(eventsSubscription))
            {
                var errorMessage = "Not authorized to create a subscription with subject " + eventsSubscription.AlternativeSubjectFilter;
                return (null, new ServiceError(401, errorMessage));
            }

            Subscription subscription = await _repository.FindSubscription(eventsSubscription, CancellationToken.None);

            subscription ??= await _repository.CreateSubscription(eventsSubscription);

            await _queue.PostSubscriptionValidation(JsonSerializer.Serialize(subscription));
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
        public async Task<List<Subscription>> GetOrgSubscriptions(string source, string subject, string type)
        {
            List<Subscription> searchresult = await _repository.GetSubscriptionsByConsumer("/org/%", false);
            return searchresult.Where(s =>
                CheckIfSourceURIPathSegmentsMatch(source, s.SourceFilter) &&
                (s.SubjectFilter == null || s.SubjectFilter.Equals(subject)) &&
                (s.TypeFilter == null || s.TypeFilter.Equals(type))).ToList();
        }

        /// <inheritdoc/>
        public async Task<List<Subscription>> GetSubscriptions(string source, string subject, string type)
        {
            return await _repository.GetSubscriptionsExcludeOrg(source, subject, type);
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

        private static bool CheckIfSourceURIPathSegmentsMatch(string source, Uri sourceFilter)
        {
            Uri sourceUri;

            if (!Uri.TryCreate(source, UriKind.Absolute, out sourceUri))
            {
                return false;
            }

            if (!sourceUri.Scheme.Equals(sourceFilter.Scheme) ||
                !sourceUri.Host.Equals(sourceFilter.Host) ||
                sourceFilter.Segments.Length > sourceUri.Segments.Length)
            {
                return false;
            }

            foreach (var segments in sourceUri.Segments.Zip(sourceFilter.Segments, (s1, s2) => new { S1 = s1, S2 = s2 }))
            {
                if (!segments.S1.Equals(segments.S2))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Enriches the subject filter with party information based on alternative subject
        /// </summary>
        private async Task EnrichSubject(Subscription eventsSubscription)
        {
            try
            {
                if (string.IsNullOrEmpty(eventsSubscription.SubjectFilter)
                    && !string.IsNullOrEmpty(eventsSubscription.AlternativeSubjectFilter))
                {
                    eventsSubscription.SubjectFilter = await GetPartyFromAlternativeSubject(eventsSubscription.AlternativeSubjectFilter);
                }
            }
            catch
            {
                // The values is not valid. To protect against washing ssn we hide it and later give a warning about invalid subject
            }
        }

        private bool ValidateSubscription(Subscription eventsSubscription, out string message)
        {
            if (string.IsNullOrEmpty(eventsSubscription.SubjectFilter)
                && string.IsNullOrEmpty(_claimsPrincipalProvider.GetUser().GetOrg()))
            {
                message = "A valid subject to the authenticated identity is required";
                return false;
            }

            if (!string.IsNullOrEmpty(eventsSubscription.AlternativeSubjectFilter)
                && string.IsNullOrEmpty(eventsSubscription.SubjectFilter))
            {
                message = "A valid subject to the authenticated identity is required";
                return false;
            }

            if (string.IsNullOrEmpty(eventsSubscription.Consumer))
            {
                message = "Missing event consumer";
                return false;
            }

            if (eventsSubscription.SourceFilter == null)
            {
                message = "Source is required";
                return false;
            }

            if (!Uri.IsWellFormedUriString(eventsSubscription.SourceFilter.ToString(), UriKind.Absolute))
            {
                message = "SourceFilter must be an absolute URI";
                return false;
            }

            string absolutePath = eventsSubscription.SourceFilter.AbsolutePath;
            if (string.IsNullOrEmpty(absolutePath) || absolutePath.Split("/").Length != 3)
            {
                message = "A valid app id is required in Source filter {environment}/{org}/{app}";
                return false;
            }

            if (string.IsNullOrEmpty(eventsSubscription.CreatedBy))
            {
                message = "Invalid creator";
                return false;
            }

            message = null;
            return true;
        }

        /// <summary>
        /// Validate that the identity (user, organization or org) is authorized to create subscriptions for given consumer. Currently
        /// it needs to match. In future we need to add validation of business rules. (yet to be defined)
        /// </summary>
        private static bool AuthorizeIdentityForConsumer(Subscription eventsSubscription)
        {
            if (!eventsSubscription.CreatedBy.Equals(eventsSubscription.Consumer))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the identity (user, organization or org) is authorized to create subscriptions for a given subject.
        ///  Subscriptions created by org do not need subject.
        /// </summary>
        private async Task<bool> AuthorizeSubjectForConsumer(Subscription eventsSubscription)
        {
            if (eventsSubscription.CreatedBy.StartsWith(UserPrefix))
            {
                int userId = Convert.ToInt32(eventsSubscription.CreatedBy.Replace(UserPrefix, string.Empty));
                UserProfile profile = await _profile.GetUserProfile(userId);
                string ssn = PersonPrefix + profile.Party.SSN;

                if (ssn.Equals(eventsSubscription.AlternativeSubjectFilter))
                {
                    return true;
                }

                bool hasRoleAccess = await _authorization.AuthorizeConsumerForEventsSubcription(eventsSubscription);

                if (hasRoleAccess)
                {
                    return true;
                }
            }
            else if (eventsSubscription.CreatedBy.StartsWith(OrgPrefix) && string.IsNullOrEmpty(eventsSubscription.SubjectFilter))
            {
                return true;
            }
            else if (eventsSubscription.CreatedBy.StartsWith(PartyPrefix) && !string.IsNullOrEmpty(eventsSubscription.SubjectFilter) && eventsSubscription.SubjectFilter.Equals(eventsSubscription.Consumer))
            {
                return true;
            }

            return false;
        }

        private async Task EnrichConsumer(Subscription eventsSubscription)
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

        private async Task SetCreatedBy(Subscription eventsSubscription)
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

        private async Task<string> GetPartyFromAlternativeSubject(string alternativeSubject)
        {
            int partyId = 0;

            if (alternativeSubject.StartsWith(OrganisationPrefix))
            {
                string orgNo = alternativeSubject.Replace(OrganisationPrefix, string.Empty);
                partyId = await _register.PartyLookup(orgNo, null);
            }
            else if (alternativeSubject.StartsWith(PersonPrefix))
            {
                string personNo = alternativeSubject.Replace(PersonPrefix, string.Empty);
                partyId = await _register.PartyLookup(null, personNo);
            }

            if (partyId != 0)
            {
                return "/party/" + partyId;
            }

            return null;
        }
    }
}
