using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using CloudNative.CloudEvents;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Extensions;

public static class RetryableEventWrapperExtensions
{
    private static readonly System.Text.Json.JsonSerializerOptions _serializerOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

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
    /// <param name="item"></param>
    /// <param name="eventWrapper"></param>
    /// <returns></returns>
    public static CloudEvent ExtractCloudEvent(this RetryableEventWrapper retryableEventWrapper)
    {
        return retryableEventWrapper.Payload.DeserializeToCloudEvent();
    }

    /// <summary>
    /// Extracts the CloudEventEnvelope from a RetryableEventWrapper payload.
    /// </summary>
    /// <param name="retryableEventWrapper"></param>
    /// <returns></returns>
    public static CloudEventEnvelope ExtractCloudEventEnvelope(this RetryableEventWrapper retryableEventWrapper)
    {
       return CloudEventEnvelope.DeserializeToCloudEventEnvelope(retryableEventWrapper.Payload);
    }
}
