using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Wolverine.Commands;

using CloudNative.CloudEvents;

using Wolverine;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes the registration event to Azure Service Bus.
/// </summary>
public class RegistrationEventPublisher(IMessageBus bus) : IRegistrationEventPublisher
{
    /// <inheritdoc/>
    public async Task PublishRegistrationEvent(CloudEvent cloudEvent, string idempotencyId)
    {
        string payload = cloudEvent.Serialize();
        await bus.SendAsync(new RegisterEventCommand(payload, idempotencyId));
    }
}
