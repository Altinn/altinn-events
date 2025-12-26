using System;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// The model used in the request for registering a new cloud event.
    /// </summary>
    public class AppCloudEventRequestModel
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        /// <summary>
        /// Gets or sets the source of the event.
        /// </summary>
        [JsonPropertyName("source")]
        public Uri Source { get; set; }

        /// <summary>
        /// Gets or sets the specification version of the event.
        /// </summary>
        [JsonPropertyName("specversion")]
        public string SpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the type of the event.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the subject of the event.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the alternative subject of the event.
        /// </summary>
        [JsonPropertyName("alternativesubject")]
        public string AlternativeSubject { get; set; }

        /// <summary>
        /// Gets or sets the cloudEvent data content. The event payload.
        /// The payload depends on the type and the dataschema.
        /// </summary>
        [JsonPropertyName("data")]
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets the cloudEvent dataschema attribute.
        /// A link to the schema that the data attribute adheres to.
        /// </summary>
        [JsonPropertyName("dataschema")]
        public Uri DataSchema { get; set; }

        /// <summary>
        /// Gets or sets the cloudEvent datacontenttype attribute.
        /// Content type of the data attribute value.
        /// </summary>
        [JsonPropertyName("contenttype")]
        public ContentType DataContentType { get; set; }

        /// <summary>
        /// Serializes the cloud event request to a JSON string.
        /// </summary>
        /// <returns>Serialized cloud event request</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this, _jsonSerializerOptions);
        }

        /// <summary>
        /// Validated required properties Source, SpecVersion, Type and Subject
        /// </summary>
        /// <returns>A boolean indicating whether all required fields have a value</returns>
        public bool ValidateRequiredProperties()
        {
            if (Source == null ||
               string.IsNullOrEmpty(Source.OriginalString) ||
               string.IsNullOrEmpty(Type) ||
               string.IsNullOrEmpty(Subject) ||
               string.IsNullOrEmpty(SpecVersion))
            {
                return false;
            }

            return true;
        }
    }
}
