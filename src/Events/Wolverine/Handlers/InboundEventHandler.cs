using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Wolverine.Commands;

using Wolverine.Attributes;

namespace Altinn.Platform.Events.Wolverine.Handlers;

/// <summary>
/// Handles inbound event commands by forwarding inbound events to the outbound service.
/// </summary>
[WolverineHandler]
public static class InboundEventHandler
{
    /// <summary>
    /// Handles the processing of an event command by checking subscriptions and posting the inbound event to the outbound service if authorized.
    /// Deserializes the CloudEvent payload before processing.
    /// </summary>
    /// <param name="message">The inbound event command containing the serialized event payload.</param>
    /// <param name="outboundService">The outbound service responsible for posting the event.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task Handle(InboundEventCommand message, IOutboundService outboundService, CancellationToken cancellationToken)
    {
        var cloudEvent = message.Payload.Deserialize();
        await outboundService.PostOutbound(cloudEvent, cancellationToken, true);
    }
}
