#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ABAC.Constants;
using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;

using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;

using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// An implementation of the <see cref="IAuthorization"/> interface that handles authorization for events.
/// </summary>
public class AuthorizationService : IAuthorization
{
    private const string _originalSubjectKey = "originalsubjectreplacedforauthorization";

    private readonly IPDP _pdp;
    private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
    private readonly IRegisterService _registerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationService"/> class with provided dependencies.
    /// </summary>
    /// <param name="pdp">A client implementation for policy decision point.</param>
    /// <param name="claimsPrincipalProvider">A service that can provide the ClaimsPrincipal for active user.</param>
    /// <param name="registerService">A service that can perform obtain more party details.</param>
    public AuthorizationService(
        IPDP pdp,
        IClaimsPrincipalProvider claimsPrincipalProvider,
        IRegisterService registerService)
    {
        _pdp = pdp;
        _claimsPrincipalProvider = claimsPrincipalProvider;
        _registerService = registerService;
    }

    /// <inheritdoc/>
    public async Task<List<CloudEvent>> AuthorizeAltinnAppEvents(List<CloudEvent> cloudEvents)
    {
        ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();
        XacmlJsonRequestRoot xacmlJsonRequest = AppCloudEventXacmlMapper.CreateMultiDecisionReadRequest(consumer, cloudEvents);
        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

        return FilterAuthorizedRequests(cloudEvents, consumer, response);
    }

    /// <inheritdoc/>
    public async Task<List<CloudEvent>> AuthorizeEvents(
        IEnumerable<CloudEvent> cloudEvents, CancellationToken cancellationToken)
    {
        ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();

        /********
         * Authorization doesn't support event subject on the format urn:altinn:person:identifier-no:08895699684.
         * We need to obtain the party UUID and use that instead when building the authorization request.
         *******/

        List<string> unsupportedSubjects = cloudEvents
            .Where(UnsupportedSubject)
            .Select(e => e.Subject!) // The where clause ensures that Subject is not null
            .Distinct()
            .ToList();

        if (unsupportedSubjects.Count > 0)
        {
            IEnumerable<PartyIdentifiers> partyIdentifiersList = 
                await _registerService.PartyLookup(unsupportedSubjects, cancellationToken);

            foreach (CloudEvent cloudEvent in cloudEvents.Where(UnsupportedSubject))
            {
                ReplaceSubject(cloudEvent, partyIdentifiersList);
            }
        }

        XacmlJsonRequestRoot xacmlJsonRequest = 
            GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(consumer, "subscribe", cloudEvents.ToList());
        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

        List<CloudEvent> authorizedEvents = FilterAuthorizedRequests(cloudEvents.ToList(), consumer, response);

        foreach (CloudEvent cloudEvent in authorizedEvents)
        {
            RestoreSubject(cloudEvent);
        }

        return authorizedEvents;
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizePublishEvent(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        ClaimsPrincipal consumer = _claimsPrincipalProvider.GetUser();

        if (consumer.HasRequiredScope(AuthorizationConstants.SCOPE_EVENTS_ADMIN_PUBLISH))
        {
            return true;
        }

        if (UnsupportedSubject(cloudEvent))
        {
            IEnumerable<PartyIdentifiers> partyIdentifiersList =
                await _registerService.PartyLookup([cloudEvent.Subject!], cancellationToken);
            ReplaceSubject(cloudEvent, partyIdentifiersList);
        }

        XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateDecisionRequest(consumer, "publish", cloudEvent);

        RestoreSubject(cloudEvent);

        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

        return IsPermit(response);
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizeConsumerForGenericEvent(
        CloudEvent cloudEvent, string consumer, CancellationToken cancellationToken)
    {
        if (UnsupportedSubject(cloudEvent))
        {
            IEnumerable<PartyIdentifiers> partyIdentifiersList =
                await _registerService.PartyLookup([cloudEvent.Subject!], cancellationToken);

            ReplaceSubject(cloudEvent, partyIdentifiersList);
        }

        XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateDecisionRequest(consumer, "subscribe", cloudEvent);

        RestoreSubject(cloudEvent);

        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

        return IsPermit(response);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, bool>> AuthorizeMultipleConsumersForGenericEvent(
        CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        Dictionary<string, bool> results = [];

        if (UnsupportedSubject(cloudEvent))
        {
            IEnumerable<PartyIdentifiers> partyIdentifiersList =
                await _registerService.PartyLookup([cloudEvent.Subject!], cancellationToken);

            ReplaceSubject(cloudEvent, partyIdentifiersList);
        }

        XacmlJsonRequestRoot xacmlJsonRequest = GenericCloudEventXacmlMapper.CreateMultiDecisionRequestForMultipleConsumers(cloudEvent, consumers, "subscribe");

        RestoreSubject(cloudEvent);

        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);

        foreach (var r in response.Response)
        {
            var consumer = ExtractConsumerFromResult(r);

            if (string.IsNullOrEmpty(consumer))
            {
                continue;
            }

            results[consumer] = r.Decision.Equals(XacmlContextDecision.Permit.ToString());
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, bool>> AuthorizeMultipleConsumersForAltinnAppEvent(
        CloudEvent cloudEvent, List<string> consumers)
    {
        var results = new Dictionary<string, bool>();
        XacmlJsonRequestRoot xacmlJsonRequest = AppCloudEventXacmlMapper.CreateMultiDecisionRequestForMultipleConsumers(cloudEvent, consumers);
        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);
            
        foreach (var r in response.Response)
        {
            var consumer = ExtractConsumerFromResult(r);

            if (string.IsNullOrEmpty(consumer))
            {
                continue;
            }

            results[consumer] = r.Decision.Equals(XacmlContextDecision.Permit.ToString());
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizeConsumerForEventsSubscription(Subscription subscription)
    {
        XacmlJsonRequestRoot xacmlJsonRequest = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);
        XacmlJsonResponse response = await _pdp.GetDecisionForRequest(xacmlJsonRequest);
        return IsPermit(response);
    }

    /// <summary>
    /// Composes a list of events that the consumer is authorized to receive based on the provided xacml response
    /// </summary>
    internal static List<CloudEvent> FilterAuthorizedRequests(List<CloudEvent> cloudEvents, ClaimsPrincipal consumer, XacmlJsonResponse response)
    {
        List<CloudEvent> authorizedEventsList = [];

        foreach (XacmlJsonResult result in response.Response.Where(result => DecisionHelper.ValidateDecisionResult(result, consumer)))
        {
            string eventId = string.Empty;

            // Loop through all attributes in Category from the response
            foreach (var attributes in result.Category.Select(category => category.Attribute))
            {
                foreach (var attribute in attributes.Where(attribute => attribute.AttributeId.Equals(AltinnXacmlUrns.EventId)))
                {
                    eventId = attribute.Value;
                }
            }

            // Find the instance that has been validated to add it to the list of authorized instances.
            CloudEvent authorizedEvent = cloudEvents.First(i => i.Id == eventId);
            authorizedEventsList.Add(authorizedEvent);
        }

        return authorizedEventsList;
    }

    private static bool IsPermit(XacmlJsonResponse response)
    {
        if (response.Response[0].Decision.Equals(XacmlContextDecision.Permit.ToString()))
        {
            return true;
        }

        return false;
    }

    private static bool UnsupportedSubject(CloudEvent e)
    {
        return e.Subject is not null && e.Subject.StartsWith("urn:altinn:person:identifier-no:");
    }

    private static void ReplaceSubject(CloudEvent cloudEvent, IEnumerable<PartyIdentifiers> partyIdentifiersList)
    {
        if (!UnsupportedSubject(cloudEvent))
        {
            return;
        }

        // Backup the original subject in the event
        cloudEvent[_originalSubjectKey] = cloudEvent.Subject!;

        // The subject is in the format urn:altinn:person:identifier-no:08895699684
        string nationalIdentityNumber = cloudEvent.Subject!.Replace("urn:altinn:person:identifier-no:", string.Empty);

        PartyIdentifiers? partyIdentifiers =
            partyIdentifiersList.FirstOrDefault(p => p.PersonIdentifier == nationalIdentityNumber);

        if (partyIdentifiers is not null)
        {
            cloudEvent.Subject = $"urn:altinn:party:uuid:{partyIdentifiers.PartyUuid}";
        }
        else
        {
            // If the party is not found in register, it's a data quality issue across systems.
            // For example a difference in the data between Dialogporten and Register.
            cloudEvent.Subject = null;
        }
    }

    private static void RestoreSubject(CloudEvent cloudEvent)
    {
        if (cloudEvent[_originalSubjectKey] is not null)
        {
            cloudEvent.Subject = cloudEvent[_originalSubjectKey]!.ToString();
            cloudEvent[_originalSubjectKey] = null;
        }
    }

    /// <summary>
    /// Extracts the consumer identifier from XACML response attributes.
    /// </summary>
    /// <param name="result">The XACML JSON result containing the decision and attributes.</param>
    /// <returns>The consumer identifier in its original format (e.g., "/party/123"), or null if not found.</returns>
    private static string? ExtractConsumerFromResult(XacmlJsonResult result)
    {
        // Find the access subject category
        var accessSubjectCategory = result.Category.FirstOrDefault(c => 
            c.CategoryId.Equals(XacmlConstants.MatchAttributeCategory.Subject));

        if (accessSubjectCategory == null)
        {
            return null;
        }

        // Check all attributes in the access subject category
        foreach (var attribute in accessSubjectCategory.Attribute)
        {
            // Map known subject attribute IDs back to consumer format
            string? consumer = attribute.AttributeId switch
            {
                "urn:altinn:partyid" => $"/party/{attribute.Value}",
                "urn:altinn:org" => $"/org/{attribute.Value}",
                "urn:altinn:userid" => $"/user/{attribute.Value}",
                "urn:altinn:systemuser:uuid" => $"/systemuser/{attribute.Value}",
                "urn:altinn:organization:identifier-no" => $"/organisation/{attribute.Value}",
                _ => null
            };

            if (consumer != null)
            {
                return consumer;
            }
        }

        return null;
    }
}
