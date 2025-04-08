using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Represents a type that implements most business logic for handling events.
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
        /// Post cloud event to registration queue.
        /// </summary>
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
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, string resource, List<string> type, string unit, string person, int size = 50);

        /// <summary>
        /// Gets a list of cloud events based on a set of filter parameters.
        /// </summary>
        /// <param name="resource">A unique resource id of an event source.</param>
        /// <param name="after">A unique id of a specific event as a starting point for the search.</param>
        /// <param name="subject">A specific event subject to filter by.</param>
        /// <param name="alternativeSubject">A specific alternative subject to filter by.</param>
        /// <param name="types">A list of event types to filter by.</param>
        /// <param name="size">The number of events to retrieve to limit the size of the list.</param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
        /// </param>
        Task<List<CloudEvent>> GetEvents(
            string resource,
            string after,
            string subject, 
            string alternativeSubject, 
            List<string> types, 
            int size,
            CancellationToken cancellationToken);
    }
}
