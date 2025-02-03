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

        (string applicationOwnerId, string appName, string instanceOwnerPartyId, string instanceGuid) = AppCloudEventExtensions.GetPropertiesFromAppSource(cloudEvent.Source);

        request.AccessSubject.Add(new XacmlJsonCategory().AddSubjectAttribute(subject));
        request.Action.Add(CloudEventXacmlMapper.CreateActionCategory("read"));
        request.Resource.Add(CreateEventsResourceCategory(applicationOwnerId, appName, instanceOwnerPartyId, instanceGuid));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }

    /// <summary>
    /// Creates a resource category for events based on the provided parameters.
    /// </summary>
    /// <param name="applicationOwnerId">The identifier of the application owner.</param>
    /// <param name="appName">The name of the application.</param>
    /// <param name="instanceOwnerPartyId">The identifier of the instance owner party.</param>
    /// <param name="instanceGuid">The GUID of the instance.</param>
    /// <param name="includeResult">A boolean indicating whether to include the result in the attributes.</param>
    /// <returns>A <see cref="XacmlJsonCategory"/> object representing the resource category for events.</returns>
    private static XacmlJsonCategory CreateEventsResourceCategory(string applicationOwnerId, string appName, string instanceOwnerPartyId, string instanceGuid, bool includeResult = false)
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

        if (!string.IsNullOrWhiteSpace(applicationOwnerId))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, applicationOwnerId, defaultType, defaultIssuer));
        }

        if (!string.IsNullOrWhiteSpace(appName))
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, appName, defaultType, defaultIssuer));
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
            string appName = paths[2];
            string eventId = cloudEvent.Id;
            string applicationOwnerId = paths[1];
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
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrgId, applicationOwnerId, defaultType, defaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.AppId, appName, defaultType, defaultIssuer));
            resourceCategory.Id = $"{CloudEventXacmlMapper.ResourceId}{counter}";
            resourcesCategories.Add(resourceCategory);

            counter++;
        }

        return resourcesCategories;
    }
}
