using System.Text;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace EventCreator.Clients;

/// <summary>
/// Extension methods for cloud events
/// </summary>
public static class CloudEventExtensions
{
    /// <summary>
    /// Serializes the cloud event using a JsonEventFormatter
    /// </summary>
    /// <returns>The json serialized cloud event</returns>
    public static string Serialize(this CloudEvent cloudEvent)
    {
        CloudEventFormatter formatter = new JsonEventFormatter();
        var bytes = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
        return Encoding.UTF8.GetString(bytes.Span);
    }
}
