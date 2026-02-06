using System.Threading;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Represents the requirements of an outbound sericve implementation with the capability to
/// identify subscriptions and queue events for delivery.
/// </summary>
public interface IOutboundService
{
    /// <summary>
    /// Finds subscriptions that match the given event and queues the event for delivery to those subscriptions.
    /// </summary>
    /// <param name="cloudEvent">The CloudEvent to be processed.</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
    /// </param>
    /// <param name="useAzureServiceBus">Indicates whether to use Azure Service Bus for event delivery.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PostOutbound(CloudEvent cloudEvent, CancellationToken cancellationToken, bool useAzureServiceBus = false);
}
