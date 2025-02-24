#nullable enable

using System;
using System.Net;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Data transfer object for posting a log event after receiving a webhook response.
    /// </summary>
    public record LogEntryDto
    {
        /// <summary>
        /// The cloud event id associated with the logged event <see cref="CloudEvent"/>"/>
        /// </summary>
        public string? CloudEventId { get; set; }

        /// <summary>
        /// The resource associated with the cloud event <see cref="CloudEvent"/>
        /// </summary>
        public string? CloudEventResource { get; set; }

        /// <summary>
        /// The type associated with the logged event <see cref="CloudEvent"/>
        /// </summary>
        public string? CloudEventType { get; set; }

        /// <summary>
        /// The subscription id associated with the post action. <see cref="Subscription"/>"/>
        /// </summary>
        public int? SubscriptionId { get; set; }

        /// <summary>
        /// The consumer of the event <see cref="Subscription"/>
        /// </summary>
        public string? Consumer { get; set; }

        /// <summary>
        /// The consumers webhook endpoint <see cref="Subscription"/>
        /// </summary>
        public Uri? Endpoint { get; set; }

        /// <summary>
        /// The staus code returned from the subscriber endpoint
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }
    }
}
