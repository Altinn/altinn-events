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
        /// Assign Id and Time values and post cloud event to registration queue.
        /// </summary>
        /// <remarks>
        /// "time" is an optional Cloud Event property, according to the official spec.
        /// From the spec docs:
        ///   https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#notational-conventions
        ///   As an Intermediary, we SHOULD forward OPTIONAL attributes
        /// </remarks>
        /// <param name="cloudEvent">The cloudEvent to be queued</param>
        /// <returns>Id for the queued event</returns>
        Task<string> RegisterNew(CloudEvent cloudEvent);

        /// <summary>
        /// Push cloud event to inbound queue.
        /// Should only be attempted after saving to persistent storage. 
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be queued</param>
        /// <returns>Id for the queued event</returns>
        Task<string> PostInbound(CloudEvent cloudEvent);

        /// <summary>
        /// Gets list of cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50);
    }
}
