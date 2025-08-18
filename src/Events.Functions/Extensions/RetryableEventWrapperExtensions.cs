using System.Text.Json;
using Altinn.Platform.Events.Functions.Models;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Functions.Extensions;

/// <summary>
/// Extension methods for <see cref="RetryableEventWrapper"/> to facilitate serialization and deserialization.
/// </summary>
public static class RetryableEventWrapperExtensions
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Deserializes the specified JSON string into a <see cref="RetryableEventWrapper"/> object.
    /// </summary>
    /// <remarks>This method uses JSON deserialization to convert the input string into a <see
    /// cref="RetryableEventWrapper"/> object.  If the input string is not a valid JSON representation of a <see
    /// cref="RetryableEventWrapper"/>, the method returns <see langword="null"/>.</remarks>
    /// <param name="item">The JSON string to deserialize. Must represent a valid <see cref="RetryableEventWrapper"/> object.</param>
    /// <returns>A <see cref="RetryableEventWrapper"/> instance if the deserialization is successful; otherwise, <see
    /// langword="null"/> if the input is invalid or deserialization fails.</returns>
    public static RetryableEventWrapper? DeserializeToRetryableEventWrapper(this string item)
    {
        try
        {
            var eventWrapper = JsonSerializer.Deserialize<RetryableEventWrapper>(item, _serializerOptions);
            return eventWrapper;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Support extracting CloudEvent from RetryableEventWrapper or directly from the item for backwards compatibility.
    /// </summary>
    /// <param name="retryableEventWrapper">The wrapper object containing the cloud event</param>
    /// <returns></returns>
    public static CloudEvent ExtractCloudEvent(this RetryableEventWrapper retryableEventWrapper)
    {
        return retryableEventWrapper.Payload.DeserializeToCloudEvent();
    }

    /// <summary>
    /// Extracts the CloudEventEnvelope from a RetryableEventWrapper payload.
    /// </summary>
    /// <param name="retryableEventWrapper">The wrapper object containing the cloud event envelope</param>
    /// <returns></returns>
    public static CloudEventEnvelope ExtractCloudEventEnvelope(this RetryableEventWrapper retryableEventWrapper)
    {
       return CloudEventEnvelope.DeserializeToCloudEventEnvelope(retryableEventWrapper.Payload);
    }
}
