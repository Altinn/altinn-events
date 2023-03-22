using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Utility class for converting Events to XACML request
    /// </summary>
    public static class GenericCloudEventXacmlMapper
    {
        /// <summary>
        /// Create XACML request for multiple 
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="events">The list of events</param>
        public static XacmlJsonRequestRoot CreateMultiDecisionRequest(ClaimsPrincipal user, List<CloudEvent> events)
        {
            List<string> actionTypes = new() { "subscribe" };
            var resourceCategory = CreateMultipleResourceCategory(events);
            return CloudEventXacmlMapper.CreateMultiDecisionRequest(user, actionTypes, resourceCategory);
        }

        private static List<XacmlJsonCategory> CreateMultipleResourceCategory(List<CloudEvent> events)
        {
            string defaultType = CloudEventXacmlMapper.DefaultType;
            string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

            List<XacmlJsonCategory> resourcesCategories = new();
            int counter = 1;

            foreach (CloudEvent cloudEvent in events)
            {
                XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:resource", cloudEvent["resource"].ToString(), defaultType, defaultIssuer));
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:resourceinstance", cloudEvent["resourceinstance"].ToString(), defaultType, defaultIssuer));
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:eventtype", cloudEvent.Type, defaultType, defaultIssuer));
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:eventsource", cloudEvent.Source.ToString(), defaultType, defaultIssuer));
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventId, cloudEvent.Id, defaultType, defaultIssuer, true));

                resourceCategory.Id = CloudEventXacmlMapper.ResourceId + counter.ToString();
                resourcesCategories.Add(resourceCategory);
                counter++;
            }

            return resourcesCategories;
        }
    }
}
