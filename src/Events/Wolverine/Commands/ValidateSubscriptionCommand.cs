using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Wolverine.Commands;

/// <summary>
/// Represents a command to validate a subscription.
/// </summary>
public record ValidateSubscriptionCommand(
    Subscription Subscription);
