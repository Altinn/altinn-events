using System.Text;

using Azure.Storage.Queues;

using CloudNative.CloudEvents;

using EventCreator.Clients;
using EventCreator.Configuration;

namespace EventCreator.Publishing;

/// <summary>
/// Publishes to the legacy Azure Storage Queue registration queue.
/// </summary>
public class AzureStorageQueueEventPublisher(QueueStorageSettings settings) : IEventPublisher
{
    private QueueClient? _registrationQueueClient;

    public async Task PublishAsync(CloudEvent cloudEvent)
    {
        QueueClient client = await GetRegistrationQueueClient();
        string serializedCloudEvent = cloudEvent.Serialize();
        await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedCloudEvent)));
    }

    private async Task<QueueClient> GetRegistrationQueueClient()
    {
        if (_registrationQueueClient is null)
        {
            _registrationQueueClient = new QueueClient(settings.ConnectionString, settings.RegistrationQueueName);
            await _registrationQueueClient.CreateIfNotExistsAsync();
        }

        return _registrationQueueClient;
    }
}
