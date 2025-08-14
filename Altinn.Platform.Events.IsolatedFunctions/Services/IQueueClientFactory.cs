using Azure.Storage.Queues;

namespace Altinn.Platform.Events.IsolatedFunctions.Services
{
    public interface IQueueClientFactory
    {
        QueueClient GetPoisonQueueClient(string queueName);
        QueueClient GetQueueClient(string queueName);
    }
}