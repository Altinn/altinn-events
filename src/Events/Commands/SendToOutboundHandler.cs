using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles inbound event commands by forwarding inbound events to the outbound service.
/// </summary>
public static class SendToOutboundHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static WolverineSettings Settings { get; set; } = null!;

    /// <summary>
    /// Configures error handling for the inbound queue handler.
    /// Retries on HTTP, database, and Service Bus exceptions.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        if (Settings == null)
        {
            throw new InvalidOperationException("WolverineSettings must be set before handler configuration");
        }

        var policy = Settings.InboundQueuePolicy;

        chain
            .OnException<HttpRequestException>() // Authorization service errors when validating event against subscriptions
            .Or<TimeoutException>() // HTTP or database timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<InvalidOperationException>() // PostgreSQL database errors when querying subscriptions
            .Or<TaskCanceledException>() // Database timeout or cancellation
            .Or<ServiceBusException>() // Azure Service Bus errors when publishing to outbound queue
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

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
