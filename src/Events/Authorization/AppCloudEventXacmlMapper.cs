using System;
using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Extensions;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Authorization;

/// <summary>
/// Utility class for converting Events to XACML request.
/// </summary>
public static class AppCloudEventXacmlMapper
{
    /// <summary>
    /// Creates an XACML request for multiple events using the claims principal user as the subject.
    /// </summary>
    /// <param name="user">The claims principal user.</param>
    /// <param name="events">The list of events.</param>
    /// <returns>A <see cref="XacmlJsonRequestRoot"/> object representing the XACML request.</returns>
    public static XacmlJsonRequestRoot CreateMultiDecisionReadRequest(ClaimsPrincipal user, List<CloudEvent> events)
    {
        List<string> actionTypes = ["read"];
        var resourceCategories = CreateMultipleResourceCategory(events);
        return CloudEventXacmlMapper.CreateMultiDecisionRequest(user, actionTypes, resourceCategories);
    }

    /// <summary>
    /// Creates a decision request based on a cloud event and subject.
    /// </summary>
    /// <param name="cloudEvent">The cloud event.</param>
    /// <param name="subject">The subject.</param>
    /// <returns>A <see cref="XacmlJsonRequestRoot"/> object representing the decision request.</returns>
    public static XacmlJsonRequestRoot CreateDecisionRequest(CloudEvent cloudEvent, string subject)
    {
        XacmlJsonRequest request = new()
        {
            Action = [],
            Resource = [],
            AccessSubject = [],
        };

        (string orgId, string appId, string instanceOwnerPartyId, string instanceGuid) = AppCloudEventExtensions.GetPropertiesFromAppSource(cloudEvent.Source);

        request.AccessSubject.Add(new XacmlJsonCategory().AddSubjectAttribute(subject));
        request.Action.Add(CloudEventXacmlMapper.CreateActionCategory("read"));
        request.Resource.Add(CreateEventsResourceCategory(orgId, appId, instanceOwnerPartyId, instanceGuid));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }

    /// <summary>
    /// Creates an events resource category.
    /// </summary>
    /// <param name="orgId">The organization ID.</param>
    /// <param name="appId">The application ID.</param>
    /// <param name="instanceOwnerPartyId">The instance owner party ID.</param>
    /// <param name="instanceGuid">The instance GUID.</param>
    /// <param name="includeResult">Indicates whether to include the result.</param>
    /// <returns>A <see cref="XacmlJsonCategory"/> object representing the events resource category.</returns>
    private static XacmlJsonCategory CreateEventsResourceCategory(string orgId, string appId, string instanceOwnerPartyId, string instanceGuid, bool includeResult = false)
    {
        string defaultType = CloudEventXacmlMapper.DefaultType;
        string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

        XacmlJsonCategory resourceCategory = new()
        {
            Attribute = []
        };

        if (!string.IsNullOrWhiteSpace(instanceOwnerPartyId))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, instanceOwnerPartyId, defaultType, defaultIssuer, includeResult));
        }

        if (!string.IsNullOrWhiteSpace(instanceGuid) && !string.IsNullOrWhiteSpace(instanceOwnerPartyId))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.InstanceId, instanceOwnerPartyId + "/" + instanceGuid, defaultType, defaultIssuer, includeResult));
        }

        if (!string.IsNullOrWhiteSpace(orgId))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, orgId, defaultType, defaultIssuer));
        }

        if (!string.IsNullOrWhiteSpace(appId))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, appId, defaultType, defaultIssuer));
        }

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", defaultType, defaultIssuer));

        return resourceCategory;
    }

    /// <summary>
    /// Creates multiple resource categories for the provided list of cloud events.
    /// </summary>
    /// <param name="events">The list of cloud events.</param>
    /// <returns>A list of <see cref="XacmlJsonCategory"/> objects representing the resource categories.</returns>
    private static List<XacmlJsonCategory> CreateMultipleResourceCategory(List<CloudEvent> events)
    {
        List<XacmlJsonCategory> resourcesCategories = [];
        string defaultType = CloudEventXacmlMapper.DefaultType;
        string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

        int counter = 1;
        foreach (CloudEvent cloudEvent in events)
        {
            Uri source = cloudEvent.Source;
            string path = source.PathAndQuery;
            string[] paths = path.Split("/");

            if (paths.Length != 6)
            {
                continue;
            }

            // This is the scenario for events related to a given instance.
            string appId = paths[2];
            string eventId = cloudEvent.Id;
            string organizationId = paths[1];
            string instanceId = paths[4] + "/" + paths[5];
            string instanceOwnerPartyId = cloudEvent.Subject.Split("/")[2];

            XacmlJsonCategory resourceCategory = new() { Attribute = [] };

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.InstanceId, instanceId, defaultType, defaultIssuer, true));
            }

            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppResource, "events", defaultType, defaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.EventId, eventId, defaultType, defaultIssuer, true));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, instanceOwnerPartyId, defaultType, defaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, organizationId, defaultType, defaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, appId, defaultType, defaultIssuer));
            resourceCategory.Id = $"{CloudEventXacmlMapper.ResourceId}{counter}";
            resourcesCategories.Add(resourceCategory);

            counter++;
        }

        return resourcesCategories;
    }
}
