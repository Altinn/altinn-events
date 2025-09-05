using System;
using System.Text.Json;

using Altinn.Platform.Events.Extensions;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Outbound processing state object
    /// </summary>
    public class CloudEventEnvelope
    {
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

            var partalSerializedEnvelope = JsonSerializer.Serialize(this, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            var index = partalSerializedEnvelope.LastIndexOf('}');
            string serializedEnvelope = partalSerializedEnvelope.Insert(index, $", \"CloudEvent\":{serializedCloudEvent}");

            CloudEvent = cloudEvent;
            return serializedEnvelope;
        }
    }
}
