using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platorm.Events.Extensions;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// The authorization service
    /// </summary>
    public class AuthorizationService : IAuthorization
    {
        private readonly IPDP _pdp;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationService"/> class.
        /// </summary>
        /// <param name="pdp">The policy decision point</param>
        /// <param name="claimsPrincipalProvider">The claims principal provider</param>
        public AuthorizationService(IPDP pdp, IClaimsPrincipalProvider claimsPrincipalProvider)
        {
            _pdp = pdp;
            _claimsPrincipalProvider = claimsPrincipalProvider;
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> AuthorizeAltinnAppEvents(List<CloudEvent> cloudEvents)
        {
            ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();
            XacmlJsonRequestRoot xacmlJsonRequest = AppCloudEventXacmlMapper.CreateMultiDecisionReadRequest(consumer, cloudEvents);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

            return FilterAuthorizedRequests(cloudEvents, consumer, response);
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> AuthorizeEvents(List<CloudEvent> cloudEvents)
        {
            ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();

            XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(consumer, "subscribe", cloudEvents);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

            return FilterAuthorizedRequests(cloudEvents, consumer, response);
        }

        /// <inheritdoc/>
        public async Task<bool> AuthorizePublishEvent(CloudEvent cloudEvent)
        {
            ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();

            if (consumer.HasRequiredScope(AuthorizationConstants.SCOPE_EVENTS_ADMIN_PUBLISH))
            {
                return true;
            }

            XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateDecisionRequest(consumer, "publish", cloudEvent);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

            return ValidateResult(response);
        }

        /// <inheritdoc/>
        public async Task<bool> AuthorizeConsumerForGenericEvent(CloudEvent cloudEvent, string consumer)
        {
            XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateDecisionRequest(consumer, "subscribe", cloudEvent);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);
            return ValidateResult(response);
        }

        /// <inheritdoc/>
        public async Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer)
        {
            XacmlJsonRequestRoot xacmlJsonRequest = AppCloudEventXacmlMapper.CreateDecisionRequest(cloudEvent, consumer);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);
            return ValidateResult(response);
        }
       
        /// <inheritdoc/>
        public async Task<bool> AuthorizeConsumerForEventsSubcription(Subscription subscription)
        {
            XacmlJsonRequestRoot xacmlJsonRequest = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);
            XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);
            return ValidateResult(response);
        }

        private static bool ValidateResult(XacmlJsonResponse response)
        {
            if (response.Response[0].Decision.Equals(XacmlContextDecision.Permit.ToString()))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Composes a list of events that the consumer is authorized to receive based on the provided xacml response
        /// </summary>
        internal static List<CloudEvent> FilterAuthorizedRequests(List<CloudEvent> cloudEvents, ClaimsPrincipal consumer, XacmlJsonResponse response)
        {
            List<CloudEvent> authorizedEventsList = new();

            foreach (XacmlJsonResult result in response.Response.Where(result => DecisionHelper.ValidateDecisionResult(result, consumer)))
            {
                string eventId = string.Empty;

                // Loop through all attributes in Category from the response
                foreach (var attributes in result.Category.Select(category => category.Attribute))
                {
                    foreach (var attribute in attributes.Where(attribute => attribute.AttributeId.Equals(AltinnXacmlUrns.EventId)))
                    {
                        eventId = attribute.Value;
                    }
                }

                // Find the instance that has been validated to add it to the list of authorized instances.
                CloudEvent authorizedEvent = cloudEvents.First(i => i.Id == eventId);
                authorizedEventsList.Add(authorizedEvent);
            }

            return authorizedEventsList;
        }
    }
}
