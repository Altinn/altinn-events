namespace Altinn.Platform.Events.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for the queue storage.
    /// </summary>
    public class QueueStorageSettings
    {
        /// <summary>
        /// ConnectionString for the storage account
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Use RegistrationQueue instead of storing directly to db.
        /// </summary>
        public bool UseRegistrationQueue { get; set; }

        /// <summary>
        /// Name of the queue to immediately register incoming events to, before validation and persistence.
        /// </summary>
        public string RegistrationQueueName { get; set; }

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
