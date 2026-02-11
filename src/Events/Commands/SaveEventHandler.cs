using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles saving of event commands.
/// </summary>
public static class SaveEventHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static WolverineSettings Settings { get; set; } = null!;

    /// <summary>
    /// Configures error handling for the registration queue handler.
    /// Retries on database and Service Bus exceptions.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        if (Settings == null)
        {
            throw new InvalidOperationException("WolverineSettings must be set before handler configuration");
        }

        var policy = Settings.RegistrationQueuePolicy;

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

    /// <summary>
    /// Handles the registration of an event command.
    /// Deserializes the CloudEvent payload before processing.
    /// </summary>
    public static async Task Handle(RegisterEventCommand message, IEventsService eventsService, CancellationToken cancellationToken)
    {
        var cloudEvent = CloudEventExtensions.Deserialize(message.Payload);
        await eventsService.SaveAndPublish(cloudEvent, cancellationToken);
    }
}
