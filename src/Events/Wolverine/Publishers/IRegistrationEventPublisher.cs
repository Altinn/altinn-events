using System;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes a registration event, either to Azure Service Bus or the legacy Storage Queue.
/// </summary>
public interface IRegistrationEventPublisher
{
    /// <summary>
    /// Publishes the registration event for the given cloud event.
    /// </summary>
    Task PublishRegistrationEvent(CloudEvent cloudEvent, string idempotencyId);
}
