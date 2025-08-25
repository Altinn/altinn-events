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
    public static IServiceCollection AddQueueSenders(
        this IServiceCollection services,
        IConfiguration config)
    {
        string conn = config["QueueStorage"]
            ?? throw new InvalidOperationException("Queue storage connection not configured.");

        var queueName = config["ExponentialRetryBackoff:QueueName"] ?? throw new InvalidOperationException("Exponential retry backoff queue name not configured.");
        string queueUsingExponentialBackoff = queueName;
        string poisonQueueUsingExponentialBackoff = queueUsingExponentialBackoff + "-poison";

        QueueClientOptions options = new()
        {
            MessageEncoding = QueueMessageEncoding.Base64,
        };

        var mainClient = new QueueClient(conn, queueUsingExponentialBackoff, options);
        mainClient.CreateIfNotExists();
        var poisonClient = new QueueClient(conn, poisonQueueUsingExponentialBackoff, options);
        poisonClient.CreateIfNotExists();

        // both delegates will point to SendMessageAsync method of their respective clients
        QueueSendDelegate mainDelegate = mainClient.SendMessageAsync;
        PoisonQueueSendDelegate poisonDelegate = poisonClient.SendMessageAsync;

        services.AddSingleton(mainDelegate);
        services.AddSingleton(poisonDelegate);

        return services;
    }
}
