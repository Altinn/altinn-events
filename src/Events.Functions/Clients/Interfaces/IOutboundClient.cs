using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Models;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to Events Outbound queue API
    /// </summary>
    public interface IOutboundClient
    {
        /// <summary>
        /// Send cloudEvent for outbound processing.
        /// </summary>
        /// <param name="item">CloudEvent to send</param>
         Task PostOutbound(CloudEvent item);
    }
}
