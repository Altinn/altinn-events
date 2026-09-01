using Altinn.Platform.Storage.Interface.Models;

using CloudNative.CloudEvents;

namespace EventCreator.Publishing;

/// <summary>
/// Builds the <see cref="CloudEvent"/> for an instance event, independent of how it's published.
/// </summary>
public static class CloudEventFactory
{
    public const string AppResourceTemplate = "urn:altinn:resource:app_{0}";

    public static CloudEvent Create(string eventType, Instance instance, string resourceBaseAddress)
    {
        string? alternativeSubject = null;
        if (!string.IsNullOrWhiteSpace(instance.InstanceOwner.OrganisationNumber))
        {
            alternativeSubject = $"/org/{instance.InstanceOwner.OrganisationNumber}";
        }

        if (!string.IsNullOrWhiteSpace(instance.InstanceOwner.PersonNumber))
        {
            alternativeSubject = $"/person/{instance.InstanceOwner.PersonNumber}";
        }

        string baseUrl = FormattedExternalAppBaseUrl(resourceBaseAddress, instance.Org, instance.AppId);

        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Subject = $"/party/{instance.InstanceOwner.PartyId}",
            Type = eventType,
            Time = DateTime.UtcNow,
            Source = new Uri($"{baseUrl}/instances/{instance.InstanceOwner.PartyId}/{instance.Id}"),
        };

        cloudEvent.SetAttributeFromString("resource", string.Format(AppResourceTemplate, instance.AppId.Replace('/', '_')));
        cloudEvent.SetAttributeFromString("resourceinstance", $"{instance.InstanceOwner.PartyId}/{instance.Id}");

        if (!string.IsNullOrEmpty(alternativeSubject))
        {
            cloudEvent.SetAttributeFromString("alternativesubject", alternativeSubject);
        }

        return cloudEvent;
    }

    private static string FormattedExternalAppBaseUrl(string resourceBaseAddress, string org, string appId)
    {
        string appHostUrl = string.Format(resourceBaseAddress, org);
        return $"{appHostUrl}/{appId}";
    }
}
