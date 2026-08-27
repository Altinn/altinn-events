using System.Net.Sockets;

using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Wolverine.Commands;

using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Functions.Wolverine.Policies;

/// <summary>
/// Configures error handling for the <see cref="OutboundEventCommand"/> handler chain.
/// </summary>
public class OutboundEventHandlerPolicy(FunctionsWolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(OutboundEventCommand))
            ?? throw new InvalidOperationException($"No handler chain found for {nameof(OutboundEventCommand)}.");

        var policy = settings.OutboundQueuePolicy;

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
}
