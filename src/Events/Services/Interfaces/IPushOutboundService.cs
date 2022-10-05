using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Defines the methods required for an implementation of the push service.
    /// </summary>
    public interface IPushOutboundService
    {
        /// <summary>
        /// Push an event to all consumers subscribing to source and/or subject
        /// </summary>
        Task PushOutbound(CloudEvent cloudEvent);
    }
}