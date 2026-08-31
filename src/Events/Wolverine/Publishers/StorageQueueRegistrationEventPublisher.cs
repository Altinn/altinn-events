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
    /// <summary>
    /// Publishes the registration event to the legacy Storage Queue.
    /// </summary>
    /// <remarks>
    /// This is the legacy Storage Queue path, which is being phased out in favor of Azure Service Bus.
    /// The <paramref name="idempotencyId"/> parameter is accepted to satisfy the
    /// <see cref="IRegistrationEventPublisher"/> contract, but idempotency is not supported on this path;
    /// the value is ignored and not forwarded to the queue.
    /// </remarks>
    /// <param name="cloudEvent">The cloud event to publish.</param>
    /// <param name="idempotencyId">Accepted for interface compatibility only; not used.</param>
    public async Task PublishRegistrationEvent(CloudEvent cloudEvent, string idempotencyId)
    {
        string payload = cloudEvent.Serialize();
        QueuePostReceipt receipt = await queueClient.EnqueueRegistration(payload);

        if (!receipt.Success)
        {
            throw receipt.Exception;
        }
    }
}
