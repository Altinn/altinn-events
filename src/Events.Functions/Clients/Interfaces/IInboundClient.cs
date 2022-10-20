using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Models;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to Events Inbound queue API
    /// </summary>
    public interface IInboundClient
    {
        /// <summary>
        /// Send cloudEvent for outbound processing.
        /// </summary>
        /// <param name="item">CloudEvent to send</param>
        Task PostInbound(CloudEvent item);
    }
}
