using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface to talk to the events service
    /// </summary>
    public interface IAppEventsService
    {
        /// <summary>
        /// Store cloud event to the events database.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be stored</param>
        /// <returns>Id for the created document</returns>
        Task<string> SaveToDatabase(CloudEvent cloudEvent);

        /// <summary>
        /// Push cloud event to inbound queue. Should only be attempted after storing event in db.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be queued</param>
        /// <returns>Id for the queued event</returns>
        Task<string> PushToInboundQueue(CloudEvent cloudEvent);

        /// <summary>
        /// Stores a cloud event document to the events database and
        /// forwards to Inbound queue in one operation. 
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be stored</param>
        /// <returns>Id for the created document</returns>
        Task<string> SaveAndPushToInboundQueue(CloudEvent cloudEvent);

        /// <summary>
        /// Gets list of cloud event based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50);
    }
}
