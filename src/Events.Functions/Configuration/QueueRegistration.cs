using System;
using Altinn.Platform.Events.Functions.Queues;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Functions.Configuration;

/// <summary>
/// Registers queue senders for the main and poison queues in the provided service collection.
/// </summary>
/// <remarks>This method configures and registers delegates for sending messages to a main queue and a
/// corresponding poison queue. The connection string for the queue storage is retrieved from the configuration using
/// the key "QueueStorage" or "QueueStorageSettings:ConnectionString". If neither is configured, an <see
/// cref="InvalidOperationException"/> is thrown.  The main queue name is retrieved from the configuration using the key
/// "QueueStorageSettings:OutboundQueueName", defaulting to "events-outbound" if not specified. The poison queue name is
/// derived by appending "-poison" to the main queue name.  The method ensures that both the main and poison queues are
/// created if they do not already exist. Delegates for sending messages to these queues are registered as singletons in
/// the service collection.</remarks>
public static class QueueRegistration
{
    /// <summary>
    /// Adds queue sender delegates for the main and poison queues to the service collection.
    /// </summary>
    /// <remarks>This method configures and registers two delegates for sending messages to Azure Storage
    /// Queues: one for the main queue and one for the poison queue. The connection string and queue names are retrieved
    /// from the provided <paramref name="config"/>. If the connection string is not configured, an <see
    /// cref="InvalidOperationException"/> is thrown. <para> The main queue name is retrieved from the
    /// "QueueStorageSettings:OutboundQueueName" configuration key, defaulting to "events-outbound" if not specified.
    /// The poison queue name is derived by appending "-poison" to the main queue name. </para> <para> The method
    /// ensures that both the main and poison queues are created if they do not already exist. </para></remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the queue sender delegates will be added.</param>
    /// <param name="config">The <see cref="IConfiguration"/> instance used to retrieve queue storage settings.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue storage connection string is not configured in the provided <paramref name="config"/>.</exception>
    public static IServiceCollection AddQueueSenders(
        this IServiceCollection services,
        IConfiguration config)
    {
        string conn = config.GetConnectionString("QueueStorage")
            ?? config["QueueStorageSettings:ConnectionString"]
            ?? throw new InvalidOperationException("Queue storage connection not configured.");

        string mainQueue = config["QueueStorageSettings:OutboundQueueName"] ?? "events-outbound";
        string poisonQueue = mainQueue + "-poison";

        var mainClient = new QueueClient(conn, mainQueue);
        mainClient.CreateIfNotExists();
        var poisonClient = new QueueClient(conn, poisonQueue);
        poisonClient.CreateIfNotExists();

        QueueSendDelegate mainDelegate = async (msg, vis, ttl, ct) =>
            await mainClient.SendMessageAsync(msg, vis, ttl, ct);

        PoisonQueueSendDelegate poisonDelegate = async (msg, vis, ttl, ct) =>
            await poisonClient.SendMessageAsync(msg, vis, ttl, ct);

        services.AddSingleton(mainDelegate);
        services.AddSingleton(poisonDelegate);

        return services;
    }
}
