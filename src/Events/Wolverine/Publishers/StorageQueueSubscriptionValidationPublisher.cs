using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes the subscription validation event to the legacy Storage Queue.
/// </summary>
public class StorageQueueSubscriptionValidationPublisher(IEventsQueueClient queueClient) : ISubscriptionValidationPublisher
{
    /// <inheritdoc/>
    public async Task PublishValidationEvent(Subscription subscription)
    {
        QueuePostReceipt receipt = await queueClient.EnqueueSubscriptionValidation(JsonSerializer.Serialize(subscription));

        if (!receipt.Success)
        {
            throw receipt.Exception;
        }
    }
}
