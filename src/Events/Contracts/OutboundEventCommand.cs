namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to process an outbound event using a serialized CloudEventEnvelope payload.
/// </summary>
/// <param name="Payload">The serialized CloudEventEnvelope JSON string.</param>
public record OutboundEventCommand(
    string Payload);
