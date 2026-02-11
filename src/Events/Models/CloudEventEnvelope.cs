using System;
using System.Text.Json;
using System.Text.Json.Nodes;

using Altinn.Platform.Events.Extensions;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Outbound processing state object
    /// </summary>
    public class CloudEventEnvelope
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// The Event to push
        /// </summary>
        public CloudEvent CloudEvent { get; set; }

        /// <summary>
        /// The time the event was posted to the outbound queue
        /// </summary>
        public DateTime Pushed { get; set; }

        /// <summary>
        /// Target URI to push event
        /// </summary>
        public Uri Endpoint { get; set; }

        /// <summary>
        /// The consumer of the events
        /// </summary>
        public string Consumer { get; set; }

        /// <summary>
        /// The subscription id that matched.
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// CloudEvent property requires specialized serialization handling.
        /// Uses string manipulation to insert the serialized cloud event.
        /// </summary>
        /// <returns>A json serialized cloud envelope</returns>
        public string Serialize()
        {
            var cloudEvent = CloudEvent;
            string serializedCloudEvent = CloudEvent.Serialize();
            CloudEvent = null;

            var partalSerializedEnvelope = JsonSerializer.Serialize(this, _jsonSerializerOptions);
            var index = partalSerializedEnvelope.LastIndexOf('}');
            string serializedEnvelope = partalSerializedEnvelope.Insert(index, $", \"CloudEvent\":{serializedCloudEvent}");

            CloudEvent = cloudEvent;
            return serializedEnvelope;
        }

        /// <summary>
        /// Deserializes a JSON string into a CloudEventEnvelope.
        /// CloudEvent property requires specialized deserialization handling.
        /// </summary>
        /// <param name="serializedEnvelope">The serialized CloudEventEnvelope JSON string</param>
        /// <returns>The deserialized CloudEventEnvelope</returns>
        public static CloudEventEnvelope Deserialize(string serializedEnvelope)
        {
            var jsonNode = JsonNode.Parse(serializedEnvelope);
            var cloudEventNode = jsonNode["CloudEvent"];

            // Deserialize CloudEvent separately using CloudEvents SDK
            var cloudEvent = cloudEventNode != null
                ? CloudEventExtensions.Deserialize(cloudEventNode.ToJsonString())
                : null;

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
