using System;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// The model used in the request for registering a new cloud event.
    /// </summary>
    public class CloudEventRequestModel : CloudEventRequestModelBase
    {
        /// <summary>
        /// Gets or sets the unique id of the cloud event.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the time property.
        /// </summary>
        [JsonPropertyName("time")]
        public DateTimeOffset Time { get; set; }
    }
}
