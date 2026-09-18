using Wolverine.Attributes;

namespace EventCreator.Publishing.Wolverine;

/// <summary>
/// Mirrors <c>Altinn.Platform.Events.Wolverine.Commands.RegisterEventCommand</c> in the Events API.
/// The message identity is pinned to that type's full CLR name because Wolverine's Azure Service Bus
/// transport identifies message types on the wire by full CLR name by default, and this command must
/// route to the same handler regardless of which app's copy of the type is compiled.
/// </summary>
/// <param name="Payload">The serialized CloudEvent JSON string.</param>
[MessageIdentity("Altinn.Platform.Events.Wolverine.Commands.RegisterEventCommand")]
public record RegisterEventCommand(string Payload);
