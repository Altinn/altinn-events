using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
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
}
