using System.Text.Json;
using System.Text.Json.Nodes;

using Altinn.Platform.Events.Functions.Extensions;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Functions.Models
{
    /// <summary>
    /// Cloud event envelope to push
    /// </summary>
    public class CloudEventEnvelope
    {
        private static readonly JsonSerializerOptions _cachedJsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions _cachedIgnoreNullOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonNodeOptions _jsonNodeOptions = new() 
        { 
            PropertyNameCaseInsensitive = true 
        };

        /// <summary>
        /// The Event to push
        /// </summary>
        public CloudEvent? CloudEvent { get; set; }

        /// <summary>
        /// The time the event was pushed to queue
        /// </summary>
        public DateTime Pushed { get; set; }

        /// <summary>
        /// Target URI to push event
        /// </summary>
        public Uri? Endpoint { get; set; }

        /// <summary>
        /// The consumer of the events
        /// </summary>
        public string? Consumer { get; set; }

        /// <summary>
        /// The subscription id that matched.
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// Deserializes a serialized cloud event envelope using a specialized deserializer for the cloud event property.
        /// </summary>
        /// <returns>The cloud event envelope object</returns>
        public static CloudEventEnvelope DeserializeToCloudEventEnvelope(string serializedEnvelope)
        {
            var n = JsonNode.Parse(serializedEnvelope, _jsonNodeOptions);
            if (n == null)
            {
                throw new ArgumentException("Failed to parse serialized envelope as JSON", nameof(serializedEnvelope));
            }

            var cloudEventNode = n["cloudEvent"];
            if (cloudEventNode == null)
            {
                throw new ArgumentException("Serialized envelope does not contain a cloudEvent property", nameof(serializedEnvelope));
            }

            string serializedCloudEvent = cloudEventNode.ToString();
            var cloudEvent = serializedCloudEvent.DeserializeToCloudEvent();

            n["cloudEvent"] = null;
            CloudEventEnvelope cloudEventEnvelope = n.Deserialize<CloudEventEnvelope>(_cachedJsonSerializerOptions) 
                ?? throw new InvalidOperationException("Failed to deserialize CloudEventEnvelope");

            cloudEventEnvelope.CloudEvent = cloudEvent;

            return cloudEventEnvelope;
        }

        /// <summary>
        /// Serializes the current instance to a JSON string representation, including the CloudEvent.
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            if (CloudEvent == null)
            {
                throw new InvalidOperationException("CloudEvent cannot be null during serialization.");
            }

            var cloudEvent = CloudEvent;
            string serializedCloudEvent = CloudEvent.Serialize();
            CloudEvent = null;

            var partialSerializedEnvelope = JsonSerializer.Serialize(this, _cachedIgnoreNullOptions);
            var index = partialSerializedEnvelope.LastIndexOf('}');
            CloudEvent = cloudEvent; // Restore CloudEvent to its original state

            if (index == -1)
            {
                throw new InvalidOperationException("Invalid JSON structure during serialization.");
            }

            string serializedEnvelope = partialSerializedEnvelope.Insert(index, $", \"CloudEvent\":{serializedCloudEvent}");

            return serializedEnvelope;
        }
    }
}
