using System.Diagnostics.CodeAnalysis;

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
        /// Name of the queue to push incoming events to, before persisting to db.
        /// </summary>
        public string RegistrationQueueName { get; set; }

        /// <summary>
        /// Name of the queue to push incoming events to, after persisting to db.
        /// Serviced by EventsInbound Azure Function
        /// </summary>
        public string InboundQueueName { get; set; }

        /// <summary>
        /// Name of queue serviced by EventsOutbound Azure Function
        /// </summary>
        public string OutboundQueueName { get; set; }

        /// <summary>
        /// Queue serviced by SubscriptionValidation Azure Function
        /// </summary>
        public string ValidationQueueName { get; set; }
    }
}
