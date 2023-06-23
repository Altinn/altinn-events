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
        private const string SubjectTypeOrg = "org";
        private const string SubjectTypePerson = "person";
        private const string SubjectTypeParty = "party";

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
            return CreateDecisionRequest(XacmlMapperHelper.CreateSubjectAttributes(consumer), actionType, cloudEvent);
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
            string[] cloudEventResourceParts = SplitResourceInTwoParts(cloudEvent.GetResource());

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(cloudEventResourceParts[0], cloudEventResourceParts[1], defaultType, defaultIssuer));

            if (cloudEvent["resourceinstance"] is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, cloudEvent["resourceinstance"].ToString(), defaultType, defaultIssuer));
            }

            AddResourceReporteeAttributeFromCloudEventSubject(cloudEvent, resourceCategory);

            return resourceCategory;
        }

        private static string[] SplitResourceInTwoParts(string resource)
        {
            int index = resource.LastIndexOf(':');
            string id = resource.Substring(0, index);
            string value = resource.Substring(index + 1);

            return new string[] { id, value };
        }

        // If we have a subject property in the CloudEvent, we map the CloudEvent subject as a XACML resource attribute.
        // A recognized subject property value begins with one of the prefixes "/org/", "/person/" or "/party/", which
        // is mapped to the appropriate attribute URNs. This will enable the PDP to enrich the request
        // with roles etc. that the user has in the context of the CloudEvent subject (aka reportee). Note we do not
        // attempt to lookup the partyId if given /org/ or /person/, as this is up to the PDP to do if the particular
        // policy requires it (ie. it needs to check rules containing subject attributes for roles and/or access groups).
        //
        // Also note that this requires a XACML subject attribute that the PDP understands in order to look up the user's
        // roles/access groups for that particular reportee, typically "urn:altinn:userid". This claim is present on all
        // Altinn tokens, so it should be available in most cases, and will also in future Maskinporten-with-system-user
        // tokens.  We do not check for this here though, as the PDP might add support for handling ie. urn:altinn:ssn
        // attributes at some point.
        private static void AddResourceReporteeAttributeFromCloudEventSubject(CloudEvent cloudEvent, XacmlJsonCategory resourceCategory)
        {
            if (string.IsNullOrEmpty(cloudEvent.Subject))
            {
                return;
            }

            string defaultType = CloudEventXacmlMapper.DefaultType;
            string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

            (string subjectType, string subjectValue) = GetSubjectTypeAndValue(cloudEvent.Subject);
            if (subjectValue == null)
            {
                return;
            }

            switch (subjectType)
            {
                case SubjectTypeOrg:
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrganizationNumber, subjectValue, defaultType, defaultIssuer));
                    break;
                case SubjectTypePerson:
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.Ssn, subjectValue, defaultType, defaultIssuer));
                    break;
                case SubjectTypeParty:
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, subjectValue, defaultType, defaultIssuer));
                    break;
            }
        }

        private static (string SubjectType, string SubjectValue) GetSubjectTypeAndValue(string subject)
        {
            string[] subjectParts = subject.Split('/');
            return subjectParts.Length != 3 ? (null, null) : (subjectParts[1], subjectParts[2]);
        }
    }
}
