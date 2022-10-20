using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Models;

namespace Altinn.Platform.Events.Functions.Clients.Interfaces
{
    /// <summary>
    /// Interface to Events Storage API
    /// </summary>
    public interface IEventsStorageClient
    {
        /// <summary>
        /// Stores a cloud event document to the events database.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to be stored</param>
        /// <returns>CloudEvent as stored in the database</returns>
        Task SaveCloudEvent(CloudEvent cloudEvent);
    }
}
