using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Models;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Utility class for converting Events Subscription requests to XACML request
    /// </summary>
    public static class SubscriptionXacmlMapper
    {
        private const string DefaultIssuer = "Altinn";
        private const string DefaultType = "string";

        private const string PartyPrefix = "/party/";

        private const string ClaimPartyID = "urn:altinn:partyid";

        /// <summary>
        /// Create a decision request based on a subscription
        /// </summary>
        public static XacmlJsonRequestRoot CreateDecisionRequest(Subscription subscription)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            string org = null;
            string app = null;

            string[] pathParams = subscription.SourceFilter.AbsolutePath.Split("/");

            if (pathParams.Length > 2)
            {
                org = pathParams[1];
                app = pathParams[2];
            }

            request.AccessSubject.Add(XacmlMapperHelper.CreateSubjectAttributes(subscription.Consumer));
            request.Action.Add(CreateActionCategory("read"));
            request.Resource.Add(CreateEventsResourceCategory(org, app, subscription.SubjectFilter));

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        private static XacmlJsonCategory CreateActionCategory(string actionType, bool includeResult = false)
        {
            XacmlJsonCategory actionAttributes = new()
            {
                Attribute = new List<XacmlJsonAttribute>
                {
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionType, DefaultType, DefaultIssuer, includeResult)
                }
            };
            return actionAttributes;
        }

        private static XacmlJsonCategory CreateEventsResourceCategory(string org, string app, string subscriptionSubjectFilter)
        {
            XacmlJsonCategory resourceCategory = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
            };

            if (!string.IsNullOrWhiteSpace(org))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, org, DefaultType, DefaultIssuer));
            }

            if (!string.IsNullOrWhiteSpace(app))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, app, DefaultType, DefaultIssuer));
            }

            if (!string.IsNullOrEmpty(subscriptionSubjectFilter))
            {
                string partyId = subscriptionSubjectFilter.Replace(PartyPrefix, string.Empty);
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimPartyID, partyId, ClaimValueTypes.Integer, DefaultIssuer));                 
            }           

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", DefaultType, DefaultIssuer));

            return resourceCategory;
        }
    }
}
