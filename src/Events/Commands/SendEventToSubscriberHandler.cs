using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles outbound event commands by forwarding events to external webhooks (subscriptions).
/// </summary>
public class SendEventToSubscriberHandler
{
    /// <summary>
    /// Handles the processing of an event command by checking subscriptions and posting the inbound event to the outbound service if authorized.
    /// </summary>
    /// <param name="message">The inbound event command containing the event to be sent outbound.</param>
    /// <param name="webhookService">The webhook service responsible for posting the event to external subscribers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task Handle(OutboundEventCommand message, IWebhookService webhookService, CancellationToken cancellationToken)
    {
        await webhookService.Send(message.Envelope, cancellationToken);
    }
}
