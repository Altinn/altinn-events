using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Altinn.Platform.Events.IsolatedFunctions.Services;

/// <summary>
/// Factory for creating and managing Azure Storage Queue clients.
/// </summary>
public class QueueClientFactory : IQueueClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<QueueClientFactory> _logger;
    private readonly ConcurrentDictionary<string, QueueClient> _queueClients = new();
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueClientFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public QueueClientFactory(IConfiguration configuration, ILogger<QueueClientFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = _configuration.GetValue<string>("QueueStorageSettings:ConnectionString")
            ?? throw new ArgumentNullException("Missing Queue Storage connection string");
    }

    /// <summary>
    /// Gets a QueueClient for the specified queue.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>A QueueClient instance.</returns>
    public QueueClient GetQueueClient(string queueName)
    {
        return _queueClients.GetOrAdd(queueName, name =>
        {
            _logger.LogDebug("Creating new QueueClient for queue {QueueName}", name);
            var client = new QueueClient(_connectionString, name);
            client.CreateIfNotExists();
            return client;
        });
    }

    /// <summary>
    /// Gets a QueueClient for the poison queue of the specified queue.
    /// </summary>
    /// <param name="queueName">The name of the original queue.</param>
    /// <returns>A QueueClient instance for the poison queue.</returns>
    public QueueClient GetPoisonQueueClient(string queueName)
    {
        string poisonQueueName = $"{queueName}-poison";
        return GetQueueClient(poisonQueueName);
    }

    /// <summary>
    /// Gets a QueueClient with a custom connection string.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="connectionString">The custom connection string to use.</param>
    /// <returns>A QueueClient instance.</returns>
    public QueueClient GetQueueClientWithCustomConnection(string queueName, string connectionString)
    {
        string cacheKey = $"{connectionString}:{queueName}";
        return _queueClients.GetOrAdd(cacheKey, _ =>
        {
            _logger.LogDebug("Creating new QueueClient for queue {QueueName} with custom connection", queueName);
            var client = new QueueClient(connectionString, queueName);
            client.CreateIfNotExists();
            return client;
        });
    }
}
