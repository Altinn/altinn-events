using System;

namespace Altinn.Platform.Events.Wolverine.Commands;

/// <summary>
/// Represents a command to register a new event using a serialized CloudEvent payload.
/// </summary>
/// <param name="Payload">The serialized CloudEvent JSON string.</param>
/// <param name="IdempotencyKey">The idempotency key to ensure the command is processed only once.</param>
public record RegisterEventCommand(
    string Payload,
    Guid? IdempotencyKey);
