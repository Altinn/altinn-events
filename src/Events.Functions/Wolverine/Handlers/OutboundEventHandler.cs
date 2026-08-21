using Altinn.Platform.Events.Functions.Wolverine.Commands;
using Altinn.Platform.Events.Functions.Wolverine.Extensions;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;

using Wolverine.Attributes;

namespace Altinn.Platform.Events.Functions.Wolverine.Handlers;

/// <summary>
/// Handles outbound event commands by forwarding events to external webhooks (subscriptions).
/// </summary>
[WolverineHandler]
public static class OutboundEventHandler
{
    /// <summary>
    /// Handles the processing of an outbound event command by sending the event to external webhooks.
    /// Deserializes the CloudEventEnvelope payload before processing.
    /// </summary>
    /// <param name="message">The outbound event command containing the serialized envelope payload.</param>
    /// <param name="webhookService">The webhook service responsible for posting the event to external subscribers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task Handle(OutboundEventCommand message, IWebhookService webhookService, CancellationToken cancellationToken)
    {
        var envelope = message.Payload.DeserializeToEnvelope();
        await webhookService.Send(envelope, cancellationToken);
    }
}
