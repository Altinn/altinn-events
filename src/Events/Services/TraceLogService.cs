using System;
using System.Threading.Tasks;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service for handling trace logs.
    /// </summary>
    public class TraceLogService(ITraceLogRepository traceLogRepository) : ITraceLogService
    {
        private readonly ITraceLogRepository _traceLogRepository = traceLogRepository;

        /// <summary>
        /// Log that a new event has been registered.
        /// </summary>
        /// <param name="cloudEvent">Contains the event data <see cref="CloudEvent"/>></param>
        /// <returns></returns>
        public async Task CreateTraceLogRegisteredEntry(CloudEvent cloudEvent)
        {
            var traceLogEntry = new TraceLog
            {
                CloudEventId = Guid.Parse(cloudEvent.Id),
                Resource = cloudEvent.GetResource(),
                EventType = cloudEvent.Type,
                Consumer = null, // we don't know about the consumer in this context
                SubscriberEndpoint = null, // no subscriber in this context
                SubscriptionId = null, // no subscription in this context
                Activity = TraceLogActivity.Registered
            };

            await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);
        }
    }
}
