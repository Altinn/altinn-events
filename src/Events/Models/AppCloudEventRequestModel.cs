using System;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// The model used in the request for registering a new cloud event for an Altinn App.
    /// </summary>
    public class AppCloudEventRequestModel : CloudEventRequestModelBase
    {
        /// <summary>
        /// Gets or sets the alternative subject of the event.
        /// </summary>
        [JsonPropertyName("alternativesubject")]
        public string AlternativeSubject { get; set; }
    }
}
