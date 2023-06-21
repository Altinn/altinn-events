using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Profile.Models;
using Altinn.Platorm.Events.Extensions;

using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <inheritdoc/>
    public class AppSubscriptionService : SubscriptionService, IAppSubscriptionService
    {
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
        public AppSubscriptionService(
            ISubscriptionRepository repository,
            IProfile profile,
            IAuthorization authorization,
            IRegisterService register,
            IEventsQueueClient queue,
            IClaimsPrincipalProvider claimsPrincipalProvider,
            IOptions<PlatformSettings> settings)
            : base(
                  repository,
                  register,
                  queue,
                  claimsPrincipalProvider)
        {
            _profile = profile;
            _authorization = authorization;
            _register = register;
            _claimsPrincipalProvider = claimsPrincipalProvider;
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
        {
            await EnrichSubject(eventsSubscription);

            var currentEntity = await GetEntityFromPrincipal();
            eventsSubscription.CreatedBy = currentEntity;
            eventsSubscription.Consumer = currentEntity;

            if (!ValidateSubscription(eventsSubscription, out string message))
            {
                return (null, new ServiceError(400, message));
            }

            SetResourceFilterIfEmpty(eventsSubscription);

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

            return await CompleteSubscriptionCreation(eventsSubscription);
        }

        private void SetResourceFilterIfEmpty(Subscription eventsSubscription)
        {
            eventsSubscription.ResourceFilter ??= GetResourceFilterFromSource(eventsSubscription.SourceFilter);
        }

        private static string GetResourceFilterFromSource(Uri sourceFilter)
        {
            // making assumptions about absolute path as it has been validated as app url before this point
            string[] pathParams = sourceFilter.AbsolutePath.Split("/");
            string org = pathParams[1];
            string app = pathParams[2];

            return string.Concat(AuthorizationConstants.AppResourcePrefix, org, '.', app);
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

            string absolutePath = eventsSubscription.SourceFilter?.AbsolutePath;
            if (absolutePath == null || absolutePath.Split("/").Length != 3)
            {
                message = "A valid app id is required in Source filter {environment}/{org}/{app}";
                return false;
            }

            if (!string.IsNullOrEmpty(eventsSubscription.ResourceFilter) &&
                !string.IsNullOrEmpty(eventsSubscription.SourceFilter.ToString()) &&
                !GetResourceFilterFromSource(eventsSubscription.SourceFilter).Equals(eventsSubscription.ResourceFilter))
            {
                message = "Provided resource filter and source filter are not compatible";
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
