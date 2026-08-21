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
/// Configures error handling for the <see cref="ValidateSubscriptionCommand"/> handler chain.
/// Retries on HTTP and network exceptions.
/// </summary>
public class ValidationEventHandlerPolicy(FunctionsWolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(ValidateSubscriptionCommand))
            ?? throw new InvalidOperationException($"No handler chain found for {nameof(ValidateSubscriptionCommand)}.");

        var policy = settings.ValidationQueuePolicy;

        chain
            .OnException<HttpRequestException>() // Errors when posting to subscriber webhook or calling back to the Events API
            .Or<HttpIOException>() // Errors when posting to subscriber webhook
            .Or<TimeoutException>() // HTTP timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<TaskCanceledException>() // HTTP timeout or cancellation
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }
}
