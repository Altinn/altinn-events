using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Services
{
    /// <inheritdoc/>
    public class AppSubscriptionService : SubscriptionService, IAppSubscriptionService
    {
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
        private readonly IRegisterService _register;

        private const string OrgPrefix = "/org/";
        private const string PersonPrefix = "/person/";
        private const string OrganisationPrefix = "/organisation/";

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        public AppSubscriptionService(
            ISubscriptionRepository repository,
            IAuthorization authorization,
            IRegisterService register,
            IEventsQueueClient queue,
            IClaimsPrincipalProvider claimsPrincipalProvider)
            : base(repository, authorization, queue, claimsPrincipalProvider)
        {
            _register = register;
            _claimsPrincipalProvider = claimsPrincipalProvider;
        }

        /// <inheritdoc/>
        public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
        {
            await EnrichSubject(eventsSubscription);

            string currentEntity = GetEntityFromPrincipal();
            eventsSubscription.CreatedBy = currentEntity;
            eventsSubscription.Consumer = currentEntity;

            if (!ValidateSubscription(eventsSubscription, out string message))
            {
                return (null, new ServiceError(400, message));
            }

            SetResourceFilterIfEmpty(eventsSubscription);

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

            return string.Format(AuthorizationConstants.AppResourceTemplate, org, app);
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
            if (absolutePath != null && absolutePath.Split("/").Length != 3)
            {
                message = "A valid app id is required in source filter {environment}/{org}/{app}";
                return false;
            }

            if (!string.IsNullOrEmpty(eventsSubscription.ResourceFilter) &&
                !string.IsNullOrEmpty(eventsSubscription.SourceFilter?.ToString()) &&
                !GetResourceFilterFromSource(eventsSubscription.SourceFilter).Equals(eventsSubscription.ResourceFilter))
            {
                message = "Provided resource filter and source filter are not compatible";
                return false;
            }

            message = null;
            return true;
        }

        private async Task<string> GetPartyFromAlternativeSubject(string alternativeSubject)
        {
            int partyId = 0;

            /* Both OrgPrefix and OrganisationPrefix is assumed to give us the organisation number of an
             * actor (instance owner). OrganisationPrefix is new while OrgPrefix was kept for backwards 
             * compatibility. There is a good chance that end user systems are using OrgPrefix to filter 
             * events for customers. We want to change over to OrganisationPrefix because OrgPrefix
             * is generally associated with the application owner acronym.
             */

            if (alternativeSubject.StartsWith(OrgPrefix))
            {
                string orgNo = alternativeSubject.Replace(OrgPrefix, string.Empty);
                partyId = await _register.PartyLookup(orgNo, null);
            }
            else if (alternativeSubject.StartsWith(OrganisationPrefix))
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
