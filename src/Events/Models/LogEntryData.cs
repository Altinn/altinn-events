using System;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Data transfer object for posting a log event after receiving a webhook response.
    /// </summary>
    public record LogEntryData
    {
        /// <summary>
        /// The cloud event associated with the post action <see cref="CloudEvent"/>"/>
        /// </summary>
        public CloudEvent CloudEvent { get; set; }

        /// <summary>
        /// The subscription id associated with the post action. <see cref="Subscription"/>"/>
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// The consumer of the event <see cref="Subscription"/>
        /// </summary>
        public string Consumer { get; set; }

        /// <summary>
        /// The consumers webhook endpoint <see cref="Subscription"/>
        /// </summary>
        public Uri Endpoint { get; set; }

        /// <summary>
        /// The staus code returned from the subscriber endpoint
        /// </summary>
        public int StatusCode { get; set; }
    }
}
