using System.Text;

using Altinn.Platform.Storage.Interface.Models;

using Azure.Storage.Queues;

using CloudNative.CloudEvents;
using EventCreator.Configuration;

namespace EventCreator.Clients;

public class EventsQueueClient(QueueStorageSettings settings, string resourceBaseAddress)
{
    public const string AppResourceTemplate = "urn:altinn:resource:app_{0}";

    private readonly QueueStorageSettings _settings = settings;
    private readonly string _resourceBaseAddress = resourceBaseAddress;

    private QueueClient? _registrationQueueClient;

    public async Task AddEvent(string eventType, Instance instance)
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

        var baseUrl = FormattedExternalAppBaseUrl(instance.Org, instance.AppId);

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

        string serializedCloudEvent = cloudEvent.Serialize();

        await EnqueueRegistration(serializedCloudEvent);
    }

    private async Task EnqueueRegistration(string content)
    {
        QueueClient client = await GetRegistrationQueueClient();
        await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));
    }

    private async Task<QueueClient> GetRegistrationQueueClient()
    {
        if (_registrationQueueClient == null)
        {
            _registrationQueueClient = new QueueClient(_settings.ConnectionString, _settings.RegistrationQueueName);
            await _registrationQueueClient.CreateIfNotExistsAsync();
        }

        return _registrationQueueClient;
    }

    private string FormattedExternalAppBaseUrl(string org, string appId)
    {
        string appHostUrl = string.Format(_resourceBaseAddress, org);
        string sourceUrl = $"{appHostUrl}/{appId}";
        return sourceUrl;
    }
}
