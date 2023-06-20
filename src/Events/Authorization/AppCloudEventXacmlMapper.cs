using System;
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
    public static class AppCloudEventXacmlMapper
    {
        /// <summary>
        /// Create XACML request for multiple 
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="events">The list of events</param>
        /// <returns></returns>
        public static XacmlJsonRequestRoot CreateMultiDecisionReadRequest(ClaimsPrincipal user, List<CloudEvent> events)
        {
            List<string> actionTypes = new() { "read" };

            var resourceCategory = CreateMultipleResourceCategory(events);
            return CloudEventXacmlMapper.CreateMultiDecisionRequest(user, actionTypes, resourceCategory);
        }

        /// <summary>
        /// Create a decision Request based on a cloud event and subject
        /// </summary>
        public static XacmlJsonRequestRoot CreateDecisionRequest(CloudEvent cloudEvent, string subject)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            (string org, string app, string instanceOwnerPartyId, string instanceGuid) = AppCloudEventExtensions.GetPropertiesFromAppSource(cloudEvent.Source);

            request.AccessSubject.Add(XacmlMapperHelper.CreateSubjectAttributes(subject));
            request.Action.Add(CloudEventXacmlMapper.CreateActionCategory("read"));
            request.Resource.Add(CreateEventsResourceCategory(org, app, instanceOwnerPartyId, instanceGuid));

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        private static XacmlJsonCategory CreateEventsResourceCategory(string org, string app, string instanceOwnerPartyId, string instanceGuid, bool includeResult = false)
        {
            string defaultType = CloudEventXacmlMapper.DefaultType;
            string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

            XacmlJsonCategory resourceCategory = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
            };

            if (!string.IsNullOrWhiteSpace(instanceOwnerPartyId))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, instanceOwnerPartyId, defaultType, defaultIssuer, includeResult));
            }

            if (!string.IsNullOrWhiteSpace(instanceGuid) && !string.IsNullOrWhiteSpace(instanceOwnerPartyId))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.InstanceId, instanceOwnerPartyId + "/" + instanceGuid, defaultType, defaultIssuer, includeResult));
            }

            if (!string.IsNullOrWhiteSpace(org))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, org, defaultType, defaultIssuer));
            }

            if (!string.IsNullOrWhiteSpace(app))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, app, defaultType, defaultIssuer));
            }

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", defaultType, defaultIssuer));

            return resourceCategory;
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

                Uri source = cloudEvent.Source;

                string path = source.PathAndQuery;

                string[] paths = path.Split("/");

                if (paths.Length == 6)
                {
                    // This is the scenario for events related to a given instance
                    string instanceId = paths[4] + "/" + paths[5];
                    string instanceOwnerPartyId = cloudEvent.Subject.Split("/")[2];
                    string org = paths[1];
                    string app = paths[2];
                    string eventId = cloudEvent.Id;

                    if (!string.IsNullOrWhiteSpace(instanceId))
                    {
                        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.InstanceId, instanceId, defaultType, defaultIssuer, true));
                    }

                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", defaultType, defaultIssuer));
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventId, eventId, defaultType, defaultIssuer, true));
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, instanceOwnerPartyId, defaultType, defaultIssuer));
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, org, defaultType, defaultIssuer));
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, app, defaultType, defaultIssuer));
                    resourceCategory.Id = CloudEventXacmlMapper.ResourceId + counter.ToString();
                    resourcesCategories.Add(resourceCategory);
                    counter++;
                }
            }

            return resourcesCategories;
        }
    }
}
