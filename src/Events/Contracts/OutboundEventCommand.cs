using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to process an outbound event using a <see cref="CloudEventEnvelope"/>.
/// </summary>
/// <param name="Envelope">The cloud event envelope to process.</param>
public record OutboundEventCommand(
    CloudEventEnvelope Envelope);
