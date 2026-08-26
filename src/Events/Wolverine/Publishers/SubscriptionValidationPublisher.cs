using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Wolverine.Commands;

using Wolverine;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes the subscription validation event to Azure Service Bus.
/// </summary>
public class SubscriptionValidationPublisher(IMessageBus bus) : ISubscriptionValidationPublisher
{
    /// <inheritdoc/>
    public async Task PublishValidationEvent(Subscription subscription)
    {
        await bus.SendAsync(new ValidateSubscriptionCommand(subscription));
    }
}
