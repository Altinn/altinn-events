using System;
using System.Text;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Extension methods for cloud events
    /// </summary>
    public static class CloudEventExtensions
    {
        /// <summary>
        ///  Serializes the cloud event using a JsonEventFormatter
        /// </summary>
        /// <returns>The json serialized cloud event</returns>
        public static string Serialize(this CloudEvent cloudEvent, CloudEventFormatter formatter = null)
        {
            formatter ??= new JsonEventFormatter();
            var bytes = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return Encoding.UTF8.GetString(bytes.Span);
        }

        /// <summary>
        /// Retrieves the resource from the cloud event. Returns null if it isn't defined.
        /// </summary>
        public static string GetResource(this CloudEvent cloudEvent) => cloudEvent["resource"]?.ToString();

        /// <summary>
        /// Retrieves the resource instance from the cloud event. Returns null if it isn't defined.
        /// </summary>
        public static string GetResourceInstance(this CloudEvent cloudEvent) => cloudEvent["resourceinstance"]?.ToString();

        /// <summary>
        /// Sets the resource extension attribute of the cloud event if it is undefiend
        /// </summary>
        public static void SetResourceIfNotDefined(this CloudEvent cloudEvent, string resource)
        {
            if (cloudEvent.GetResource() == null)
            {
                cloudEvent["resource"] = resource;
            }
        }
    }
}
