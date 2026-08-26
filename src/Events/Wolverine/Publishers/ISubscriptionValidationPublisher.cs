using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Wolverine.Publishers;

/// <summary>
/// Publishes a subscription validation event, either to Azure Service Bus or the legacy Storage Queue.
/// </summary>
public interface ISubscriptionValidationPublisher
{
    /// <summary>
    /// Publishes the validation event for the given subscription.
    /// </summary>
    Task PublishValidationEvent(Subscription subscription);
}
