using Wolverine.Attributes;

namespace Altinn.Platform.Events.Functions.Wolverine.Commands;

/// <summary>
/// Represents a command to process an outbound event using a serialized CloudEventEnvelope payload.
/// The message identity is pinned to match the Events API's own (unrenamed) namespace for this
/// type, since Wolverine identifies ASB message types by full CLR name by default and this
/// command must route identically regardless of which app's copy is compiled.
/// </summary>
/// <param name="Payload">The serialized CloudEventEnvelope JSON string.</param>
[MessageIdentity("Altinn.Platform.Events.Wolverine.Commands.OutboundEventCommand")]
public record OutboundEventCommand(
    string Payload);
