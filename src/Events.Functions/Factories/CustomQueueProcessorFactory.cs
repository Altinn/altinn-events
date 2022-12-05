using Microsoft.Azure.WebJobs.Host.Queues;

namespace Altinn.Platform.Events.Functions.Factories
{
    /// <summary>
    /// Custom QueueProcessorFactory 
    /// </summary>
    public class CustomQueueProcessorFactory : IQueueProcessorFactory
    {
        /// <inheritdoc/>
        public QueueProcessor Create(QueueProcessorOptions options)
        {
            if (options.Queue.Name == "events-outbound" ||
                options.Queue.Name == "subscription-validation")
            {
                options.Options.MaxDequeueCount = 12;
            }

            return new QueueProcessorWrapper(options);
        }
    }
}
