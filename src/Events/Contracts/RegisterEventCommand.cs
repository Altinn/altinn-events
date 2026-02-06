using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to register a new event using a <see cref="CloudEvent"/>.
/// </summary>
public record RegisterEventCommand(
    CloudEvent RegisterEvent);
