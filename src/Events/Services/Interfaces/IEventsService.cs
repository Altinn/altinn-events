using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Register.Contracts;

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
        /// <param name="idempotencyKey">The idempotency key for the request</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>true if the cloud event was saved successfully, otherwise false</returns>
        Task<bool> Save(CloudEvent cloudEvent, Guid? idempotencyKey = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Post cloud event to registration queue.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be queued</param>
        /// <param name="idempotencyKey">The idempotency key for the request</param>
        /// <returns>Id for the queued event</returns>
        Task<string> RegisterNew(CloudEvent cloudEvent, Guid? idempotencyKey);

        /// <summary>
        /// Gets list of app events based on query params
        /// </summary>
        /// <param name="after">Only include events registered after the event with this id.</param>
        /// <param name="from">Only include events with time at or after this value.</param>
        /// <param name="to">Only include events with time at or before this value.</param>
        /// <param name="subject">The party URN of the event subject, or null for any subject.</param>
        /// <param name="source">The event sources to include, or null or empty for any source.</param>
        /// <param name="resource">The resource to include, or null for any resource.</param>
        /// <param name="type">The event types to include, or empty for any type.</param>
        /// <param name="size">The maximum number of events to return.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
        /// <returns>The events the user is authorized to read.</returns>
        Task<List<CloudEvent>> GetAppEvents(
            string after,
            DateTime? from,
            DateTime? to,
            PartyUrn subject,
            List<string> source,
            string resource,
            List<string> type,
            int size = 50,
            CancellationToken cancellationToken = default);

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
