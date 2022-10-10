using System.Diagnostics.CodeAnalysis;

namespace Altinn.Platform.Events.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for the queue storage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class QueueStorageSettings
    {
        /// <summary>
        /// ConnectionString for the storage account
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Name of the queue to push incoming events to, after persisting to db.
        /// </summary>
        public string InboundQueueName { get; set; }

        /// <summary>
        /// Name of the queue to push outbound events to.
        /// </summary>
        public string OutboundQueueName { get; set; }

        /// <summary>
        /// Name of the queue to push new subscriptions to.
        /// </summary>
        public string ValidationQueueName { get; set; }
    }
}
