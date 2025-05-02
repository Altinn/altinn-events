#nullable enable

using System;
using System.Linq;
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
        private const string _validationType = "platform.events.validatesubscription";

        /// <inheritdoc/>
        public async Task<string> CreateRegisteredEntry(CloudEvent cloudEvent)
        {
            try
            {
                var parseResult = Guid.TryParse(cloudEvent.Id, out Guid parsedGuid);

                var traceLogEntry = new TraceLog
                {
                    CloudEventId = parseResult ? parsedGuid : null,
                    Resource = cloudEvent.GetResource(),
                    EventType = cloudEvent.Type,
                    Consumer = null, // we don't know about the consumer in this context
                    SubscriberEndpoint = null, // no subscriber in this context
                    SubscriptionId = null, // no subscription in this context
                    Activity = TraceLogActivity.Registered
                };

                await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);
                return cloudEvent.Id ?? string.Empty;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating trace log entry for registered event: {CloudEventId} with message: {Message}", cloudEvent.Id, exception.Message);

                // don't throw exception, we don't want to stop the event processing
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateLogEntryWithSubscriptionDetails(CloudEvent cloudEvent, Subscription subscription, TraceLogActivity activity)
        {
            try
            {
                var parseResult = Guid.TryParse(cloudEvent.Id, out Guid parsedGuid);

                var traceLogEntry = new TraceLog
                {
                    CloudEventId = parseResult ? parsedGuid : null,
                    Resource = cloudEvent.GetResource(),
                    EventType = cloudEvent.Type,
                    Consumer = subscription.Consumer,
                    SubscriberEndpoint = subscription.EndPoint?.ToString(),
                    SubscriptionId = subscription.Id,
                    Activity = activity
                };

                await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);

                return parseResult && cloudEvent.Id != null ? cloudEvent.Id.ToString() : string.Empty;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating trace log entry with subscription details: {Message} {CloudEventId} {SubscriptionId} {Consumer}", exception.Message, cloudEvent.Id, subscription.Id, subscription.Consumer);

                // don't throw exception, we don't want to stop the event processing
                return string.Empty;
            }
        }

        /// <summary>
        /// Log response from webhook post to subscriber.
        /// Should be called by the storage controller when a webhook POST response is received. The controller action should handle exceptions
        /// for better handling of correct status codes
        /// </summary>
        /// <param name="logEntryDto">A data transfer object passed to the controller endpoint <see cref="LogEntryDto"/></param>
        /// <returns>A string representation of the cloud event id</returns>
        public async Task<string> CreateWebhookResponseEntry(LogEntryDto logEntryDto)
        {
            var parseResult = Guid.TryParse(logEntryDto.CloudEventId, out Guid parsedGuid);

            string errorMessage = Validate(parseResult, logEntryDto);

            if (string.IsNullOrEmpty(errorMessage))
            {
                var traceLogEntry = new TraceLog
                {
                    CloudEventId = parsedGuid,
                    Resource = logEntryDto.CloudEventResource,
                    EventType = logEntryDto.CloudEventType,
                    Consumer = logEntryDto.Consumer,
                    SubscriberEndpoint = logEntryDto.Endpoint?.ToString(),
                    SubscriptionId = logEntryDto.SubscriptionId,
                    ResponseCode = (int?)logEntryDto.StatusCode,
                    Activity = DetermineActivityBasedOnTypeAndResponse(logEntryDto)
                };
                await _traceLogRepository.CreateTraceLogEntry(traceLogEntry);
                return logEntryDto.CloudEventId ?? string.Empty;
            }

            _logger.LogError("Error creating trace log entry for webhook POST response: {ErrorMessage}", errorMessage);
            return string.Empty;
        }

        private static TraceLogActivity DetermineActivityBasedOnTypeAndResponse(LogEntryDto logEntryDto)
        {
            var isValidationType = logEntryDto.CloudEventType != null && logEntryDto.CloudEventType.Equals(_validationType);

            if (isValidationType)
            {
                return logEntryDto.IsSuccessStatusCode == true ? TraceLogActivity.EndpointValidationSuccess : TraceLogActivity.EndpointValidationFailed;
            }

            return TraceLogActivity.WebhookPostResponse;
        }

        /// <summary>
        /// Validates parameters
        /// </summary>
        /// <returns>A joined string of one or more errors, or string.Empty if params are valid </returns>
        private static string Validate(bool parseResult, LogEntryDto logEntryDto)
        {
            string[] errors =
            [
                !parseResult ? "Invalid GUID" : string.Empty,
        logEntryDto.Endpoint == null ? "Missing required Endpoint parameter" : string.Empty,
        logEntryDto.StatusCode == null ? "Missing required StatusCode parameter" : string.Empty
            ];
            var aggregatedErrorMessage = string.Join(", ", errors.Where(s => !string.IsNullOrEmpty(s)));

            return aggregatedErrorMessage;
        }
    }
}
