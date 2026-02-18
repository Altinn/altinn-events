using System;
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
        private static readonly JsonSerializerOptions _cachedJsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions _cachedIgnoreNullOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonNodeOptions _jsonNodeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Serializes the CloudEventEnvelope to a JSON string, including the CloudEvent.
        /// CloudEvent property requires specialized serialization handling.
        /// </summary>
        /// <param name="envelope">The CloudEventEnvelope to serialize</param>
        /// <returns>A JSON serialized CloudEventEnvelope</returns>
        public static string Serialize(this CloudEventEnvelope envelope)
        {
            if (envelope.CloudEvent == null)
            {
                throw new InvalidOperationException("CloudEvent cannot be null during serialization.");
            }

            var cloudEvent = envelope.CloudEvent;
            string serializedCloudEvent = cloudEvent.Serialize();
            envelope.CloudEvent = null;

            var partialSerializedEnvelope = JsonSerializer.Serialize(envelope, _cachedIgnoreNullOptions);
            var index = partialSerializedEnvelope.LastIndexOf('}');
            envelope.CloudEvent = cloudEvent; // Restore CloudEvent to its original state

            if (index == -1)
            {
                throw new InvalidOperationException("Invalid JSON structure during serialization.");
            }

            string serializedEnvelope = partialSerializedEnvelope.Insert(index, $", \"CloudEvent\":{serializedCloudEvent}");

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
            ArgumentException.ThrowIfNullOrEmpty(serializedEnvelope);

            var n = JsonNode.Parse(serializedEnvelope, _jsonNodeOptions) ?? throw new ArgumentException("Failed to parse serialized envelope as JSON", nameof(serializedEnvelope));

            var cloudEventNode = n["cloudEvent"] ?? throw new ArgumentException("Serialized envelope does not contain a cloudEvent property", nameof(serializedEnvelope));

            string serializedCloudEvent = cloudEventNode.ToString();
            var cloudEvent = serializedCloudEvent.Deserialize();

            n["cloudEvent"] = null;
            CloudEventEnvelope cloudEventEnvelope = n.Deserialize<CloudEventEnvelope>(_cachedJsonSerializerOptions)
                ?? throw new InvalidOperationException("Failed to deserialize CloudEventEnvelope");

            cloudEventEnvelope.CloudEvent = cloudEvent;

            return cloudEventEnvelope;
        }
    }
}
