using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles outbound event commands by forwarding events to external webhooks (subscriptions).
/// </summary>
public class SendEventToSubscriberHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static WolverineSettings Settings { get; set; } = null!;

    /// <summary>
    /// Configures error handling for the outbound queue handler.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        if (Settings == null)
        {
            throw new InvalidOperationException("WolverineSettings must be set before handler configuration");
        }

        var policy = Settings.OutboundQueuePolicy;

        chain
            .OnException<HttpRequestException>() // Errors when posting to subscriber webhook
            .Or<HttpIOException>() // Errors when posting to subscriber webhook
            .Or<TimeoutException>() // HTTP timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<TaskCanceledException>() // Database timeout or cancellation            
            .RetryWithCooldown(policy.GetCooldownDelays()) // 10s
            .Then.ScheduleRetry(policy.GetScheduleDelays()) // 30s, 1m, 5m, 10m, 30m, 1h, 3h, 6h, 12h, 12h
            .Then.MoveToErrorQueue();
    }
    
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
