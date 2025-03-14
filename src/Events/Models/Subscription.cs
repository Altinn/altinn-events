using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Class that describes an events subscription
    /// </summary>
    public class Subscription
    {
        /// <summary>
        /// Subscription Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Endpoint to receive matching events
        /// </summary>
        public Uri EndPoint { get; set; }

        /// <summary>
        /// Filter on source
        /// </summary>
        public Uri SourceFilter { get; set; }

        /// <summary>
        /// Filter on resource
        /// </summary>
        public string ResourceFilter { get; set; }

        /// <summary>
        /// Filter on subject
        /// </summary>
        public string SubjectFilter { get; set; }

        /// <summary>
        /// Filter on alternative subject
        /// </summary>
        public string AlternativeSubjectFilter { get; set; }

        /// <summary>
        /// Filter for type. The different sources has different types. 
        /// </summary>
        public string TypeFilter { get; set; }

        /// <summary>
        /// The events consumer
        /// </summary>
        public string Consumer { get; set; }

        /// <summary>
        /// Who created this subscription
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// When subscription was created
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Indicate whether the subscription has been validated to be ok. 
        /// </summary>
        public bool Validated { get; set; }

        /// <summary>
        /// Serializes the subscription to a JSON string.
        /// </summary>
        /// <returns>Serialized cloud event</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }

        /// <summary>
        /// Deserializes the subscription from a JSON string.
        /// </summary>
        /// <returns>Cloud event</returns>
        public static Subscription Deserialize(string jsonString)
        {
            return JsonSerializer.Deserialize<Subscription>(jsonString, new JsonSerializerOptions { });
        }
    }
}
