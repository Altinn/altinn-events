using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Helpers;
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

        private const string UserPrefix = "/user/";
        private const string OrgPrefix = "/org/";
        private const string PartyPrefix = "/party/";

        private const string ClaimUserId = "urn:altinn:userid";
        private const string ClaimPartyID = "urn:altinn:partyid";
        private const string ClaimOrg = "urn:altinn:org";

        /// <summary>
        /// Create a decision Request based on a subscription, subject (subscription consumer) and resorce (subscription (alternative) subject filter)
        /// </summary>
        public static XacmlJsonRequestRoot CreateDecisionRequest(Subscription cloudEvent, string subscriptionConsumer, string subscriptionSubjectFilter)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            string org = null;
            string app = null;

            string[] pathParams = cloudEvent.SourceFilter.AbsolutePath.Split("/");

            if (pathParams.Length > 2)
            {
                org = pathParams[1];
                app = pathParams[2];
            }

            request.AccessSubject.Add(XacmlMapperHelper.CreateSubjectAttributes(subscriptionConsumer));
            request.Action.Add(CreateActionCategory("read"));
            request.Resource.Add(CreateEventsResourceCategory(org, app, subscriptionSubjectFilter));

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

            resourceCategory.Attribute.Add(CreateResourceAttribute(subscriptionSubjectFilter));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", DefaultType, DefaultIssuer));

            return resourceCategory;
        }

        private static XacmlJsonAttribute CreateResourceAttribute(string resource)
        {
            if (resource.StartsWith(UserPrefix))
            {
                string value = resource.Replace(UserPrefix, string.Empty);
                return DecisionHelper.CreateXacmlJsonAttribute(ClaimUserId, value, ClaimValueTypes.String, DefaultIssuer);
            }
            else if (resource.StartsWith(OrgPrefix))
            {
                string value = resource.Replace(OrgPrefix, string.Empty);
                return DecisionHelper.CreateXacmlJsonAttribute(ClaimOrg, value, ClaimValueTypes.String, DefaultIssuer);
            }
            else if (resource.StartsWith(PartyPrefix))
            {
                string value = resource.Replace(PartyPrefix, string.Empty);
                return DecisionHelper.CreateXacmlJsonAttribute(ClaimPartyID, value, ClaimValueTypes.Integer, DefaultIssuer);
            }

            return null;
        }
    }
}
