using System;
using System.Collections.Generic;
using System.Linq;
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
/// Configures error handling for the <see cref="RegisterEventCommand"/> handler chain.
/// Retries on database and Service Bus exceptions.
/// </summary>
public class RegistrationEventHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(RegisterEventCommand))
            ?? throw new InvalidOperationException($"No handler chain found for {nameof(RegisterEventCommand)}.");

        var policy = settings.RegistrationQueuePolicy;

        chain
            .OnException<InvalidOperationException>() // PostgreSQL database errors when saving events
            .Or<TaskCanceledException>() // Database timeout or cancellation
            .Or<TimeoutException>() // Database timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<ServiceBusException>() // Azure Service Bus errors when publishing
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }
}
