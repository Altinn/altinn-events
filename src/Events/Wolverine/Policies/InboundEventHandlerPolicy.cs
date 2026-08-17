using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Commands;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Wolverine.Policies;

/// <summary>
/// Configures error handling for the <see cref="InboundEventCommand"/> handler chain.
/// Retries on HTTP, database, and Service Bus exceptions.
/// </summary>
public class InboundEventHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(InboundEventCommand))
            ?? throw new InvalidOperationException($"No handler chain found for {nameof(InboundEventCommand)}.");

        var policy = settings.InboundQueuePolicy;

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
}
