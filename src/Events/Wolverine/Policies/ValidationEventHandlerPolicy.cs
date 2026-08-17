using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Commands;

using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Wolverine.Policies;

/// <summary>
/// Configures error handling for the <see cref="ValidateSubscriptionCommand"/> handler chain.
/// Retries on HTTP, database, and Service Bus exceptions.
/// </summary>
public class ValidationEventHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(ValidateSubscriptionCommand))
            ?? throw new InvalidOperationException($"No handler chain found for {nameof(ValidateSubscriptionCommand)}.");

        var policy = settings.ValidationQueuePolicy;

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
}
