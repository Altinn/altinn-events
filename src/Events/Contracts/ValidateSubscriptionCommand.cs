using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Contracts;

/// <summary>
/// Represents a command to validate a subscription.
/// </summary>
public record ValidateSubscriptionCommand(
    Subscription Subscription);
