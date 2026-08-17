using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Wolverine.Commands;

namespace Altinn.Platform.Events.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for processing subscription validation commands from Azure Service Bus.
/// </summary>
public static class ValidationEventHandler
{
    /// <summary>
    /// Handles the ValidateSubscriptionCommand by delegating to the subscription service.
    /// </summary>
    public static async Task Handle(ValidateSubscriptionCommand message, ISubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        await subscriptionService.SendAndValidate(message.Subscription, cancellationToken);
    }
}
