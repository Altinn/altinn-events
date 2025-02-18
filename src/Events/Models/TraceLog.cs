#nullable enable
using System;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Class that describes a trace log entry
    /// </summary>
    public class TraceLog
    {
        /// <summary>
        /// Gets or sets the unique identifier for the cloud event.
        /// </summary>
        public Guid CloudEventId { get; set; }

        /// <summary>
        /// Gets or sets the resource associated with the trace log entry.
        /// </summary>
        public string Resource { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of event being logged.
        /// </summary>
        public string EventType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the consumer associated with the trace log entry.
        /// </summary>
        public string? Consumer { get; set; }

        /// <summary>
        /// Gets or sets the endpoint of the subscriber.
        /// </summary>
        public string? SubscriberEndpoint { get; set; }

        /// <summary>
        /// Reference to the subscription that this trace log entry is associated with.
        /// </summary>
        public int? SubscriptionId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the response code returned by the subscriber.
        /// </summary>
        public int? ResponseCode { get; set; }

        /// <summary>
        /// Gets or sets the activity associated with the trace log entry.
        /// </summary>
        public TraceLogActivity Activity { get; set; }
    }
}
