using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to send an event using a <see cref="CloudEvent"/> to inbound.
/// </summary>
public record InboundEventCommand(
    CloudEvent InboundEvent);
