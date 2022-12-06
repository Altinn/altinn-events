using Microsoft.Azure.WebJobs.Host.Queues;

namespace Altinn.Platform.Events.Functions.Factories
{
    /// <summary>
    /// Custom QueueProcessorFactory 
    /// </summary>
    public class CustomQueueProcessorFactory : IQueueProcessorFactory
    {
        /// <inheritdoc/>
        public QueueProcessor Create(QueueProcessorOptions queueProcessorOptions)
        {
            if (queueProcessorOptions.Queue.Name == "events-outbound" ||
                queueProcessorOptions.Queue.Name == "subscription-validation")
            {
                queueProcessorOptions.Options.MaxDequeueCount = 12;
            }

            return new QueueProcessorWrapper(queueProcessorOptions);
        }
    }
}
