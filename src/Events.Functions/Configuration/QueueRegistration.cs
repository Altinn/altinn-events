using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Events.Functions.Queues;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Functions.Configuration;

/// <summary>
/// Registers queue senders for the main and poison queues in the provided service collection.
/// </summary>
/// <remarks>This method configures and registers delegates for sending messages to a main queue and a
/// corresponding poison queue. The connection string for the queue storage is retrieved from the configuration using
/// the key "QueueStorage". If this is not configured, an <see
/// cref="InvalidOperationException"/> is thrown. The poison queue name is
/// derived by appending "-poison" to the main queue name.  The method ensures that both the main and poison queues are
/// created if they do not already exist. Delegates for sending messages to these queues are registered as singletons in
/// the service collection.</remarks>
public static class QueueRegistration
{
    /// <summary>
    /// Adds queue sender delegates for the main and poison queues to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the queue sender delegates will be added.</param>
    /// <param name="config">The <see cref="IConfiguration"/> instance used to retrieve queue storage settings.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue storage connection string is not configured in the provided <paramref name="config"/>.</exception>
    /// <remarks>This class is excluded from code coverage because it has no logic to be tested.</remarks>
    [ExcludeFromCodeCoverage]
    public static IServiceCollection AddQueueSenders(
        this IServiceCollection services,
        IConfiguration config)
    {
        string conn = config["ServiceBusConnection"]
            ?? throw new InvalidOperationException("Service Bus connection not configured.");

        var queueName = config["ExponentialRetryBackoff:QueueName"] ?? "events-outbound";
        string queueUsingExponentialBackoff = queueName;
        string poisonQueueUsingExponentialBackoff = queueUsingExponentialBackoff + "-poison";

        // Create Service Bus client
        ServiceBusClient serviceBusClient = new ServiceBusClient(conn);

        // Create senders for main and poison queues
        ServiceBusSender mainSender = serviceBusClient.CreateSender(queueUsingExponentialBackoff);
        ServiceBusSender poisonSender = serviceBusClient.CreateSender(poisonQueueUsingExponentialBackoff);

        // Create delegates that match the signature expected by QueueSendDelegate and PoisonQueueSendDelegate
        QueueSendDelegate mainDelegate = async (message, visibilityTimeout, timeToLive, cancellationToken) =>
        {
            ServiceBusMessage serviceBusMessage = new ServiceBusMessage(message)
            {
                TimeToLive = timeToLive ?? TimeSpan.FromDays(14)
            };

            if (visibilityTimeout.HasValue)
            {
                serviceBusMessage.ScheduledEnqueueTime = DateTimeOffset.UtcNow.Add(visibilityTimeout.Value);
            }

            await mainSender.SendMessageAsync(serviceBusMessage, cancellationToken);
        };

        PoisonQueueSendDelegate poisonDelegate = async (message, visibilityTimeout, timeToLive, cancellationToken) =>
        {
            ServiceBusMessage serviceBusMessage = new ServiceBusMessage(message)
            {
                TimeToLive = timeToLive ?? TimeSpan.FromDays(14)
            };

            if (visibilityTimeout.HasValue)
            {
                serviceBusMessage.ScheduledEnqueueTime = DateTimeOffset.UtcNow.Add(visibilityTimeout.Value);
            }

            await poisonSender.SendMessageAsync(serviceBusMessage, cancellationToken);
        };

        services.AddSingleton(mainDelegate);
        services.AddSingleton(poisonDelegate);

        services.AddSingleton(serviceBusClient);
        return services;
    }
}
