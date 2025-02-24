using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for trace log service.
    /// </summary>
    public interface ITraceLogService
    {
        /// <summary>
        /// Creates a trace log entry based on registration of a new event
        /// </summary>
        /// <param name="cloudEvent">CloudNative CloudEvent <see cref="CloudEvent"/></param>
        /// <returns>A string representing the result of the asynchronous operation: Cloud event id.</returns>
        Task<string> CreateRegisteredEntry(CloudEvent cloudEvent);

        /// <summary>
        /// Log response from webhook post to subscriber.
        /// </summary>
        /// <param name="cloudEvent">Contains relevant information about the event <see cref="CloudEvent"/></param>
        /// <param name="subscriptionId">The id associated with the subscription <see cref="Subscription"/></param>
        /// <param name="consumer">The consumer of the event</param>
        /// <param name="endpoint">The consumers webhook endpoint</param>
        /// <param name="responseCode">The status code returned from the subscriber endpoint</param>
        /// <returns></returns>
        Task<string> CreateWebhookResponseEntry(CloudEvent cloudEvent, int subscriptionId, string consumer, Uri endpoint, int responseCode);

        /// <summary>
        /// Creates a trace log entry with information about cloud event and subscription
        /// </summary>
        /// <param name="cloudEvent">Cloud Event associated with log entry <see cref="CloudEvent"/></param>
        /// <param name="subscription">Subscription associated with log entry <see cref="Subscription"/></param>
        /// <param name="activity">Type of activity associated with log entry <see cref="TraceLogActivity"/></param>
        /// <returns></returns>
        Task<string> CreateLogEntryWithSubscriptionDetails(CloudEvent cloudEvent, Subscription subscription, TraceLogActivity activity);
    }
}
