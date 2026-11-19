using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Wolverine.Commands;

/// <summary>
/// Represents a command to validate a subscription.
/// </summary>
/// <param name="Subscription">The subscription to validate.</param>
public record ValidateSubscriptionCommand(
    Subscription Subscription);
