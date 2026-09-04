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
    /// <remarks>
    /// PERF TEST ONLY — do not merge to main.
    /// Body replaced with a no-op: skips <see cref="IEventsService.SaveAndPublish"/> (the
    /// Postgres write + inbound publish) so the registration queue is drained without any
    /// handler-side work, to isolate AMQP link/credit contention (shared ServiceBusClient
    /// connection) from handler-side DB/thread-pool contention. See
    /// perftesting/github-comment-followup.md for context.
    /// </remarks>
    public static Task Handle(RegisterEventCommand message, IEventsService eventsService, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
