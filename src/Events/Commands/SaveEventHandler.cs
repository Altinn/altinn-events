using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Handles saving of event commands.
/// </summary>
public static class SaveEventHandler
{
    /// <summary>
    /// Configures error handling for the registration queue handler.
    /// Retries on database and Service Bus exceptions.
    /// </summary>
    public static void Configure(HandlerChain chain, IOptions<WolverineSettings> settings)
    {
        var policy = settings.Value.RegistrationQueuePolicy;

        chain
            .OnException<NpgsqlException>() // PostgreSQL database errors
            .Or<TimeoutException>() // Database timeout
            .Or<SocketException>() // Network connectivity issues
            .Or<ServiceBusException>() // Azure Service Bus errors when publishing
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Handles the registration of an event command.
    /// </summary>
    public static async Task Handle(RegisterEventCommand message, IEventsService eventsService, CancellationToken cancellationToken)
    {
        await eventsService.SaveAndPublish(message.RegisterEvent, cancellationToken);
    }
}
