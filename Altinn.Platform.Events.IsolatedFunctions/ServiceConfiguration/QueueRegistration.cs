using Altinn.Platform.Events.Functions.Queues;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.IsolatedFunctions.ServiceConfiguration;

public static class QueueRegistration
{
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
