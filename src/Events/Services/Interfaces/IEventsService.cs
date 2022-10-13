using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface to the EventsService, used for saving events to database storage
    /// and posting to the events-inbound queue.
    /// </summary>
    public interface IEventsService
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
        /// Save a cloud event to persistent storage and
        /// pushes to Inbound events queue in one operation.
        /// Id will be assigned a new guid in string format.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be stored</param>
        /// <returns>Id for the cloudEvent in persistent storage</returns>
        [Obsolete("This function has been split in two and will be removed soon.")]
        Task<string> SaveAndPostInbound(CloudEvent cloudEvent);

        /// <summary>
        /// Gets list of cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50);
    }
}