using System.Text.Json;
using System.Text.Json.Nodes;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Extension methods for CloudEventEnvelope
    /// </summary>
    public static class CloudEventEnvelopeExtensions
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Serializes the CloudEventEnvelope to a JSON string.
        /// CloudEvent property requires specialized serialization handling.
        /// Uses string manipulation to insert the serialized cloud event.
        /// </summary>
        /// <param name="envelope">The CloudEventEnvelope to serialize</param>
        /// <returns>A JSON serialized CloudEventEnvelope</returns>
        public static string Serialize(this CloudEventEnvelope envelope)
        {
            var cloudEvent = envelope.CloudEvent;
            string serializedCloudEvent = cloudEvent.Serialize();
            envelope.CloudEvent = null;

            var partialSerializedEnvelope = JsonSerializer.Serialize(envelope, _jsonSerializerOptions);
            var index = partialSerializedEnvelope.LastIndexOf('}');
            string serializedEnvelope = partialSerializedEnvelope.Insert(index, $", \"CloudEvent\":{serializedCloudEvent}");

            envelope.CloudEvent = cloudEvent;
            return serializedEnvelope;
        }

        /// <summary>
        /// Deserializes a JSON string to a CloudEventEnvelope.
        /// CloudEvent property requires specialized deserialization handling.
        /// </summary>
        /// <param name="serializedEnvelope">The serialized CloudEventEnvelope JSON string</param>
        /// <returns>The deserialized CloudEventEnvelope</returns>
        public static CloudEventEnvelope DeserializeToEnvelope(this string serializedEnvelope)
        {
            var jsonNode = JsonNode.Parse(serializedEnvelope);
            var cloudEventNode = jsonNode["CloudEvent"];

            // Deserialize CloudEvent separately using CloudEvents SDK
            var cloudEvent = cloudEventNode?.ToJsonString().Deserialize();

            // Remove CloudEvent from the JSON to deserialize the rest
            jsonNode.AsObject().Remove("CloudEvent");

            // Deserialize the envelope without CloudEvent
            var envelope = JsonSerializer.Deserialize<CloudEventEnvelope>(jsonNode.ToJsonString(), _jsonSerializerOptions);

            // Restore the CloudEvent
            envelope.CloudEvent = cloudEvent;

            return envelope;
        }
    }
}
