using System.Net;

namespace Altinn.Platform.Events.Functions.Models
{
    /// <summary>
    /// Data transfer object for posting a log event after receiving a webhook response.
    /// </summary>
    public record LogEntryDto
    {
        /// <summary>
        /// The cloud event id associated with the logged event
        /// </summary>
        public string? CloudEventId { get; set; }

        /// <summary>
        /// The resource associated with the cloud event 
        /// </summary>
        public string? CloudEventResource { get; set; }

        /// <summary>
        /// The type associated with the logged event 
        /// </summary>
        public string? CloudEventType { get; set; }

        /// <summary>
        /// The subscription id associated with the post action.
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// The consumer of the event 
        /// </summary>
        public string? Consumer { get; set; }

        /// <summary>
        /// The consumers webhook endpoint
        /// </summary>
        public Uri? Endpoint { get; set; }

        /// <summary>
        /// The staus code returned from the subscriber endpoint
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Boolean value based on whether or not the response from the subscriber was successful
        /// </summary>
        public bool IsSuccessStatusCode { get; set; }
    }
}
