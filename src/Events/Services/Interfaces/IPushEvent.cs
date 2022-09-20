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
        /// Push a event to all consumer subcribing to source and/or subject
        /// </summary>
        Task Push(CloudEvent cloudEvent);
    }
}
