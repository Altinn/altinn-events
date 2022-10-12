using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface to talk to the events service
    /// </summary>
    public interface IInboundService
    {
        /// <summary>
        /// Save cloud event to persistent storage.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be saved</param>
        /// <returns>Id for the created document</returns>
        Task<string> Save(CloudEvent cloudEvent);

        /// <summary>
        /// Push cloud event to inbound queue.
        /// Should only be attempted after saving to persistent storage. 
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be queued</param>
        /// <returns>Id for the queued event</returns>
        Task<string> PostInbound(CloudEvent cloudEvent);

        /// <summary>
        /// [DEPRECATED] Save a cloud event to persistent storage and
        /// pushes to Inbound events queue in one operation.
        /// Id will be assigned a new guid in string format.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be stored</param>
        /// <returns>Id for the cloudEvent in persistent storage</returns>
        Task<string> SaveAndPostInbound(CloudEvent cloudEvent);

        /// <summary>
        /// Gets list of cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50);
    }
}
