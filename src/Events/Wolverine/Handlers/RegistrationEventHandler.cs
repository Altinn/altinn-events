using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Wolverine.Commands;

using Wolverine.Attributes;

namespace Altinn.Platform.Events.Wolverine.Handlers;

/// <summary>
/// Handles saving of event commands.
/// </summary>
[WolverineHandler]
public static class RegistrationEventHandler
{
    /// <summary>
    /// Handles the registration of an event command.
    /// Deserializes the CloudEvent payload before processing.
    /// </summary>
    public static async Task Handle(RegisterEventCommand message, IEventsService eventsService, CancellationToken cancellationToken)
    {
        var cloudEvent = message.Payload.Deserialize();
        await eventsService.SaveAndPublish(cloudEvent, message.IdempotencyId, cancellationToken);
    }
}
