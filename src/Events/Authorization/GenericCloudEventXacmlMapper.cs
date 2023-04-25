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
        /// Create XACML request for authorizing the provided action type on multiple events
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="actionType">The action type</param>
        /// <param name="events">The list of events</param>
        public static XacmlJsonRequestRoot CreateMultiDecisionRequest(ClaimsPrincipal user, string actionType, List<CloudEvent> events)
        {
            List<string> actionTypes = new() { actionType };
            var resourceCategory = CreateMultipleResourceCategory(events);
            return CloudEventXacmlMapper.CreateMultiDecisionRequest(user, actionTypes, resourceCategory);
        }

        /// <summary>
        /// Create XACML request for executing the provided action for the provided generic cloud event 
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="actionType">The action type</param>
        /// <param name="cloudEvent">The cloud events to publish</param>
        public static XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string actionType, CloudEvent cloudEvent)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            request.AccessSubject.Add(DecisionHelper.CreateSubjectCategory(user.Claims));
            request.Action.Add(CloudEventXacmlMapper.CreateActionCategory(actionType));
            request.Resource.Add(CreateResourceCategory(cloudEvent));

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        /// <summary>
        /// Creates a multi resource category for the provided list of cloud events
        /// </summary>
        internal static List<XacmlJsonCategory> CreateMultipleResourceCategory(List<CloudEvent> events)
        {
            List<XacmlJsonCategory> resourcesCategories = new();
            int counter = 1;

            foreach (CloudEvent cloudEvent in events)
            {
                var resourceCategory = CreateResourceCategory(cloudEvent);
                resourceCategory.Id = CloudEventXacmlMapper.ResourceId + counter.ToString();

                resourcesCategories.Add(resourceCategory);
                counter++;
            }

            return resourcesCategories;
        }

        /// <summary>
        /// Creates a resource category for a cloud event.
        /// </summary>
        /// <remarks>
        /// If id is required this should be included by the caller. 
        /// Attribute eventId is tagged with `includeInResponse`</remarks>
        internal static XacmlJsonCategory CreateResourceCategory(CloudEvent cloudEvent)
        {
            string defaultType = CloudEventXacmlMapper.DefaultType;
            string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventId, cloudEvent.Id, defaultType, defaultIssuer, true));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventType, cloudEvent.Type, defaultType, defaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventSource, cloudEvent.Source.ToString(), defaultType, defaultIssuer));
            string[] cloudEventResourceParts = SplitResourceInTwoParts(cloudEvent["resource"].ToString());

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(cloudEventResourceParts[0], cloudEventResourceParts[1], defaultType, defaultIssuer));

            if (cloudEvent["resourceinstance"] is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, cloudEvent["resourceinstance"].ToString(), defaultType, defaultIssuer));
            }

            return resourceCategory;
        }

        private static string[] SplitResourceInTwoParts(string resource)
        {
            int index = resource.LastIndexOf(':');
            string id = resource.Substring(0, index);
            string value = resource.Substring(index + 1);

            return new string[] { id, value };
        }
    }
}
