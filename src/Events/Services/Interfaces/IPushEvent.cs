using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Defines the methods required for an implementation of the push service.
    /// </summary>
    public interface IPushEvent
    {
        /// <summary>
        /// Push a event to the given consumer endpoint
        /// </summary>
        Task PushToConsumer(CloudEventEnvelope cloudEventEnvelope);

        /// <summary>
        /// Push a event to the consumer specified in the subscription
        /// </summary>
        Task Push(CloudEvent cloudEvent, Subscription subscription);
    }
}
