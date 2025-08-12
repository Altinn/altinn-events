using System;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models;

/// <summary>
/// This record contains metadata related to manual retry operations.
/// </summary>
public record RetryableEventWrapper
{
    private static readonly System.Text.Json.JsonSerializerOptions _serializerOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Contains the actual payload of the event.
    /// </summary>
    public required CloudEvent CloudEvent { get; set; }

    /// <summary>
    /// Specifies the number of times the message has been retried.
    /// </summary>
    public int DequeueCount { get; set; }

    /// <summary>
    /// Unique identifier for all events belonging to the same content, across retries.
    /// </summary>
    public string? CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp indicating when the event was first fired.
    /// </summary>
    public DateTime FirstProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Serializes the current instance to a JSON string representation.
    /// </summary>
    /// <returns></returns>
    internal string Serialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, _serializerOptions);
    }
}
