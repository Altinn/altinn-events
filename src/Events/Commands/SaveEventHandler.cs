using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles saving of event commands.
/// </summary>
public static class SaveEventHandler
{
    /// <summary>
    /// Handles the registration of an event command.
    /// </summary>
    public static async Task Handle(RegisterEventCommand message, IEventsService eventsService, CancellationToken cancellationToken)
    {
        await eventsService.SaveAndPublish(message.RegisterEvent, cancellationToken);
    }
}
