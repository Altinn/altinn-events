using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Extensions;

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
            return CreateDecisionRequest(DecisionHelper.CreateSubjectCategory(user.Claims), actionType, cloudEvent);
        }

        /// <summary>
        /// Create XACML request for executing the provided action for the provided generic cloud event 
        /// </summary>
        /// <param name="consumer">The consumer of the cloud event</param>
        /// <param name="actionType">The action type</param>
        /// <param name="cloudEvent">The cloud events to publish</param>
        public static XacmlJsonRequestRoot CreateDecisionRequest(string consumer, string actionType, CloudEvent cloudEvent)
        {
            return CreateDecisionRequest(new XacmlJsonCategory().AddSubjectAttribute(consumer), actionType, cloudEvent);
        }

        /// <summary>
        /// Creates an XACML JSON multi-decision request for multiple consumers and a single cloud event.
        /// </summary>
        /// <param name="cloudEvent">The cloud event containing event metadata used to populate resource attributes.</param>
        /// <param name="consumers">A list of consumer identifiers to include as access subjects in the request.</param>
        /// <param name="actionType">The action type to be performed (e.g., "subscribe", "publish").</param>
        /// <returns>An XacmlJsonRequestRoot object representing the constructed multi-decision request.</returns>
        internal static XacmlJsonRequestRoot CreateMultiDecisionRequestForMultipleConsumers(CloudEvent cloudEvent, List<string> consumers, string actionType)
        {
            XacmlJsonRequest request = new()
            {
                Action = [],
                Resource = [],
                AccessSubject = [],
            };

            List<XacmlJsonCategory> subjectCategories = CreateMultipleSubjectCategories(consumers);
            request.AccessSubject.AddRange(subjectCategories);

            var actionCategory = CloudEventXacmlMapper.CreateActionCategory(actionType, true);
            actionCategory.Id = $"{CloudEventXacmlMapper.ActionId}1";
            request.Action.Add(actionCategory);

            var resourceCategory = CreateResourceCategory(cloudEvent);
            resourceCategory.Id = $"{CloudEventXacmlMapper.ResourceId}1";
            request.Resource.Add(resourceCategory);

            request.MultiRequests = CloudEventXacmlMapper.CreateMultiRequestsCategory(
                request.AccessSubject,
                request.Action,
                request.Resource);

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        private static XacmlJsonRequestRoot CreateDecisionRequest(XacmlJsonCategory subjectAttributes, string actionType, CloudEvent cloudEvent)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            request.AccessSubject.Add(subjectAttributes);
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
        /// Creates multiple subject categories for the provided list of consumers.
        /// </summary>
        /// <param name="consumers">List of consumer identifiers.</param>
        /// <returns>List of XacmlJsonCategory objects representing the subjects.</returns>
        internal static List<XacmlJsonCategory> CreateMultipleSubjectCategories(List<string> consumers)
        {
            List<XacmlJsonCategory> subjectCategories = new(consumers.Count);

            for (int i = 0; i < consumers.Count; i++)
            {
                var subjectCategory = new XacmlJsonCategory().AddSubjectAttribute(consumers[i], includeInResult: true);
                subjectCategory.Id = $"{CloudEventXacmlMapper.SubjectId}{i + 1}";
                subjectCategories.Add(subjectCategory);
            }

            return subjectCategories;
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
            resourceCategory.AddAttributeFromUrn(cloudEvent.GetResource());
            resourceCategory.AddSubjectAttribute(cloudEvent.Subject); 

            if (cloudEvent["resourceinstance"] is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, cloudEvent["resourceinstance"].ToString(), defaultType, defaultIssuer));
            }

            return resourceCategory;
        }
    }
}
