using System.IO;
using System.Text;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Altinn.Platform.Events.Functions.Extensions
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
        public static string Serialize(this CloudEvent cloudEvent)
        {
            var formatter = new JsonEventFormatter();
            var bytes = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return Encoding.UTF8.GetString(bytes.Span);
        }

        /// <summary>
        ///  Deserializes a json string to a the cloud event using a JsonEventFormatter
        /// </summary>
        /// <returns>The cloud event</returns>
        public static CloudEvent DeserializeToCloudEvent(this string item)
        {
            var formatter = new JsonEventFormatter();

            var cloudEvent = formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(item)), null, null);
            return cloudEvent;
        }
    }
}
