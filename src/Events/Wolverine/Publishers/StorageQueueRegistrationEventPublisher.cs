using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes the registration event to the legacy Storage Queue.
/// </summary>
public class StorageQueueRegistrationEventPublisher(IEventsQueueClient queueClient) : IRegistrationEventPublisher
{
    /// <inheritdoc/>
    public async Task PublishRegistrationEvent(CloudEvent cloudEvent)
    {
        string payload = cloudEvent.Serialize();
        QueuePostReceipt receipt = await queueClient.EnqueueRegistration(payload);

        if (!receipt.Success)
        {
            throw receipt.Exception;
        }
    }
}
