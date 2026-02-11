namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to register a new event using a serialized CloudEvent payload.
/// </summary>
/// <param name="Payload">The serialized CloudEvent JSON string.</param>
public record RegisterEventCommand(
    string Payload);
