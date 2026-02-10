using System.Threading.Tasks;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Commands;

/// <summary>
/// Wolverine handler for processing subscription validation commands from Azure Service Bus.
/// </summary>
public class ValidateSubscriptionHandler
{
    private readonly ISubscriptionService _subscriptionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateSubscriptionHandler"/> class.
    /// </summary>
    public ValidateSubscriptionHandler(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Handles the ValidateSubscriptionCommand by delegating to the subscription service.
    /// </summary>
    public async Task Handle(ValidateSubscriptionCommand command)
    {
        Subscription subscription = command.Subscription;
        ServiceError error = await _subscriptionService.SendAndValidate(subscription);

        if (error != null)
        {
            throw new System.InvalidOperationException($"Failed to validate subscription {subscription.Id}: {error.ErrorMessage}");
        }
    }
}
