using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles inbound event commands by forwarding inbound events to the outbound service.
/// </summary>
public class SendToOutboundHandler
{
    /// <summary>
    /// Handles the processing of an event command by checking subscriptions and posting the inbound event to the outbound service if authorized.
    /// </summary>
    /// <param name="message">The inbound event command containing the event to be sent outbound.</param>
    /// <param name="outboundService">The outbound service responsible for posting the event.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task Handle(InboundEventCommand message, IOutboundService outboundService, CancellationToken cancellationToken)
    {
        await outboundService.PostOutbound(message.InboundEvent, cancellationToken, true);
    }
}
