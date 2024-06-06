using System.Collections.Generic;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Configuration;
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

        /// <summary>
        /// Create a decision request based on a subscription
        /// </summary>
        public static XacmlJsonRequestRoot CreateDecisionRequest(Subscription subscription)
        {
            bool isAppSubs = subscription.ResourceFilter.StartsWith(AuthorizationConstants.AppResourcePrefix);
            string action = isAppSubs ? "read" : "subscribe";

            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            request.AccessSubject.Add(XacmlMapperHelper.CreateSubjectAttributes(subscription.Consumer));
            request.Action.Add(CreateActionCategory(action));
            request.Resource.Add(CreateResourceCategory(subscription, isAppSubs));

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

        private static XacmlJsonCategory CreateResourceCategory(Subscription subscription, bool isAppEventSubs)
        {
            XacmlJsonCategory resourceCategory = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
            };

            resourceCategory.SetXacmlJsonAttributeFromUrn(subscription.ResourceFilter);

            if (!string.IsNullOrEmpty(subscription.SubjectFilter))
            {
                XacmlJsonCategory subjectAttributes = XacmlMapperHelper.CreateSubjectAttributes(subscription.SubjectFilter);
                resourceCategory.Attribute.AddRange(subjectAttributes.Attribute);
            }

            if (isAppEventSubs)
            {
                (_, string resourceFilterValue) = XacmlMapperHelper.SplitResourceInTwoParts(subscription.ResourceFilter);

                string[] resourceAttParts = resourceFilterValue.Split('_');
                string org = resourceAttParts[1];
                string app = resourceAttParts[2];
                AddAppResourceAttributes(resourceCategory, org, app);
            }

            return resourceCategory;
        }

        private static void AddAppResourceAttributes(XacmlJsonCategory resourceCategory, string org, string app)
        {
            if (!string.IsNullOrWhiteSpace(org))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, org, DefaultType, DefaultIssuer));
            }

            if (!string.IsNullOrWhiteSpace(app))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, app, DefaultType, DefaultIssuer));
            }

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", DefaultType, DefaultIssuer));
        }
    }
}
