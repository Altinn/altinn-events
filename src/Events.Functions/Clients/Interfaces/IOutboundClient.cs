using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Models;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to handle services exposed in Platform Events Push
    /// </summary>
    public interface IOutboundClient
    {
        /// <summary>
        /// Push cloudevent
        /// </summary>
        /// <param name="item">CloudEvent to push</param>
         Task PostOutbound(CloudEvent item);
    }
}
