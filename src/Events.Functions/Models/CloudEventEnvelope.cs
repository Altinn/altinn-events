using System;
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
        /// <summary>
        /// The Event to push
        /// </summary>
        public CloudEvent CloudEvent { get; set; }

        /// <summary>
        /// The time the event was pushed to queue
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
        /// Deserializes a serialized cloud event envelope using a specialized deserializer for the cloud event property.
        /// </summary>
        /// <returns>The cloud event envelope object</returns>
        public static CloudEventEnvelope DeserializeToCloudEventEnvelope(string serializedEnvelope)
        {
            var n = JsonNode.Parse(serializedEnvelope);
            string serializedCloudEvent = n["cloudEvent"].ToString();
            var cloudEvent = serializedCloudEvent.DeserializeToClodEvent();

            n["cloudEvent"] = null;
            CloudEventEnvelope cloudEventEnvelope = n.Deserialize<CloudEventEnvelope>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            cloudEventEnvelope.CloudEvent = cloudEvent;

            return cloudEventEnvelope;
        }
    }
}
