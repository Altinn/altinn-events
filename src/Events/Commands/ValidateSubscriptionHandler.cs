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
/// Wolverine handler for processing subscription validation commands from Azure Service Bus.
/// </summary>
public static class ValidateSubscriptionHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static EventsWolverineSettings Settings { get; set; } = null!;

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

        var policy = Settings.ValidationQueuePolicy;

        chain
            .OnException<HttpRequestException>() // Authorization service errors when validating event against subscriptions
            .Or<HttpIOException>() // Errors when posting to subscriber webhook
            .Or<TimeoutException>() // HTTP or database timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<InvalidOperationException>() // PostgreSQL database errors when querying subscriptions
            .Or<TaskCanceledException>() // Database timeout or cancellation
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Handles the ValidateSubscriptionCommand by delegating to the subscription service.
    /// </summary>
    public static async Task Handle(ValidateSubscriptionCommand message, ISubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        await subscriptionService.SendAndValidate(message.Subscription, cancellationToken);
    }
}
