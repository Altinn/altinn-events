namespace Altinn.Platform.Events.Wolverine.Commands;

/// <summary>
/// Represents a command to register a new event using a serialized CloudEvent payload.
/// </summary>
/// <param name="Payload">The serialized CloudEvent JSON string.</param>
/// <param name="IdempotencyId">The idempotency ID to ensure the command is processed only once.</param>
public record RegisterEventCommand(
    string Payload,
    string IdempotencyId);
