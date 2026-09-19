using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Models;

/// <summary>
/// Represents a single event claimed from <c>events.events</c> for processing.
/// </summary>
public class ClaimedEvent
{
    /// <summary>
    /// Gets the sequence number of the claimed event.
    /// </summary>
    public long SequenceNo { get; init; }

    /// <summary>
    /// Gets the claimed, deserialized cloud event.
    /// </summary>
    public required CloudEvent CloudEvent { get; init; }
}
