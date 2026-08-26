using Altinn.Platform.Events.Models;

using Wolverine.Attributes;

namespace Altinn.Platform.Events.Functions.Wolverine.Commands;

/// <summary>
/// Represents a command to validate a subscription. The message identity is pinned to match
/// the Events API's own (unrenamed) namespace for this type, since Wolverine identifies ASB
/// message types by full CLR name by default and this command must route identically
/// regardless of which app's copy is compiled.
/// </summary>
[MessageIdentity("Altinn.Platform.Events.Wolverine.Commands.ValidateSubscriptionCommand")]
public record ValidateSubscriptionCommand(
    Subscription Subscription);
