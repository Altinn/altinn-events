using System;
using System.Net;
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
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation: Cloud event id.</returns>
        Task<string> CreateRegisteredEntry(CloudEvent cloudEvent);

        /// <summary>
        /// Log response from webhook post to subscriber.
        /// </summary>
        /// <param name="logEntryDto">Data transfer object associated with cloud event, status code, and subscription</param>
        /// <returns>A string representation of the GUID or an empty string</returns>
        Task<string> CreateWebhookResponseEntry(LogEntryDto logEntryDto);

        /// <summary>
        /// Creates a trace log entry with information about cloud event and subscription
        /// </summary>
        /// <param name="cloudEvent">Cloud Event associated with log entry <see cref="CloudEvent"/></param>
        /// <param name="subscription">Subscription associated with log entry <see cref="Subscription"/></param>
        /// <param name="activity">Type of activity associated with log entry <see cref="TraceLogActivity"/></param>
        /// <returns>Returns empty string if a log entry can't be created</returns>
        Task<string> CreateLogEntryWithSubscriptionDetails(CloudEvent cloudEvent, Subscription subscription, TraceLogActivity activity);
    }
}
