using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Platform.Events.Functions.Extensions;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Functions.Models.Payloads
{
    /// <summary>
    /// Represents a model for sending to Slack.
    /// </summary>
    public class SlackEnvelope
    {
        /// <summary>
        /// Gets or sets the cloudevent as string.
        /// </summary>
        [JsonPropertyName("text")]
        public CloudEvent CloudEvent { get; set; }

        /// <summary>
        /// Serializes the SlackEnvelope to a JSON string.
        /// </summary>
        /// <returns>Serialized slack envelope</returns>
        public string Serialize()
        {
            string serializedCloudEvent = CloudEvent.Serialize().Replace("\"", "\\\"");
            return CloudEvent == null ? "{ }" : string.Format("{{\"text\": \"{0}\"}}", serializedCloudEvent );
        }
    }
}
