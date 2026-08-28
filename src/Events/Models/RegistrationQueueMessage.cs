using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Envelope used when publishing a cloud event to the "events-registration" queue.
    /// Carries the serialized cloud event alongside operational metadata (e.g. the
    /// idempotency id supplied by the caller) that should not be persisted as part
    /// of the cloud event itself.
    /// </summary>
    public class RegistrationQueueMessage
    {
        /// <summary>
        /// The serialized cloud event payload (JSON), produced by <see cref="Extensions.CloudEventExtensions.Serialize"/>.
        /// </summary>
        [JsonPropertyName("cloudEventPayload")]
        public string CloudEventPayload { get; set; }

        /// <summary>
        /// The idempotency id supplied by the client via the Idempotency-Id header, if any.
        /// </summary>
        [JsonPropertyName("idempotencyId")]
        public string IdempotencyId { get; set; }
    }
}
