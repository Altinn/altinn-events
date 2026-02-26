namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to send an event to inbound using a serialized CloudEvent payload.
/// </summary>
/// <param name="Payload">The serialized CloudEvent JSON string.</param>
public record InboundEventCommand(
    string Payload);
