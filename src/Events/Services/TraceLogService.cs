using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service for handling trace logs.
    /// </summary>
    public class TraceLogService(ITraceLogRepository traceLogRepository, ILogger<ITraceLogService> logger) : ITraceLogService
    {
        private readonly ITraceLogRepository _traceLogRepository = traceLogRepository;
        private readonly ILogger<ITraceLogService> _logger = logger;

        /// <inheritdoc/>
        public async Task<string> CreateRegisteredEntry(CloudEvent cloudEvent)
        {
            try
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
                return cloudEvent.Id;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating trace log entry for registered event: {Message}", exception.Message);

                // don't throw exception, we don't want to stop the event processing
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateLogEntryWithSubscriptionDetails(CloudEvent cloudEvent, Subscription subscription, TraceLogActivity activity)
        {
            try
            {
                var traceLogEntry = new TraceLog
                {
                    CloudEventId = Guid.Parse(cloudEvent.Id),
                    Resource = cloudEvent.GetResource(),
                    EventType = cloudEvent.Type,
                    Consumer = subscription.Consumer,
                    SubscriberEndpoint = null, // not relevant when unauthorized
                    SubscriptionId = subscription.Id,
                    Activity = TraceLogActivity.Unauthorized
                };

                await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);
                return cloudEvent.Id;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating trace log entry with subscription details: {Message}", exception.Message);

                // don't throw exception, we don't want to stop the event processing
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateWebhookResponseEntry(LogEntryDto logEntryDto)
        {
            try
            {
                var traceLogEntry = new TraceLog
                {
                    CloudEventId = Guid.Parse(logEntryDto.CloudEventId),
                    Resource = logEntryDto.CloudEventResource,
                    EventType = logEntryDto.CloudEventType,
                    Consumer = logEntryDto.Consumer,
                    SubscriberEndpoint = logEntryDto.Endpoint.ToString(),
                    SubscriptionId = logEntryDto.SubscriptionId,
                    ResponseCode = (int?)logEntryDto.StatusCode,
                    Activity = TraceLogActivity.WebhookPostResponse
                };
                await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);
                return logEntryDto.CloudEventId;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating trace log entry for webhook POST response: {Message}", exception.Message);
                throw;
            }
        }
    }
}
