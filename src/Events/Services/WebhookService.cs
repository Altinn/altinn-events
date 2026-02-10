using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Handles Webhook service
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly HttpClient _client;
        private readonly ITraceLogService _traceLogService;
        private readonly ILogger<WebhookService> _logger;
        private readonly string _slackUri = "hooks.slack.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        public WebhookService(
            HttpClient client, ITraceLogService traceLogService, IOptions<EventsOutboundSettings> eventOutboundSettings, ILogger<WebhookService> logger)
        {
            _client = client;
            _traceLogService = traceLogService;
            _logger = logger;
            _client.Timeout = TimeSpan.FromSeconds(eventOutboundSettings.Value.RequestTimeout);
        }

        /// <inheritdoc/>
        public async Task Send(CloudEventEnvelope envelope, CancellationToken cancellationToken)
        {
            string payload = GetPayload(envelope);
            StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _client.PostAsync(envelope.Endpoint, httpContent, cancellationToken);

                // log response from webhook to Events
                await _traceLogService.CreateWebhookResponseEntry(GetLogEntry(envelope, response.StatusCode, response.IsSuccessStatusCode));

                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("WebhookService send failed to send cloud event id {CloudEventId} {SubscriptionId} {Reason} {Response}", envelope.CloudEvent?.Id, envelope.SubscriptionId, reason, response);

                    throw new HttpRequestException(reason);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Send to webhook with {SubscriptionId} failed with error message {Message}", envelope.SubscriptionId, e.Message);
                throw;
            }
        }

        /// <summary>
        /// Prepares the provided cloud envelope as serialized payload
        /// </summary>
        internal string GetPayload(CloudEventEnvelope envelope)
        {
            if (envelope.Endpoint?.OriginalString.Contains(_slackUri) == true)
            {
                SlackEnvelope slackEnvelope = new()
                {
                    CloudEvent = envelope.CloudEvent
                };
                return slackEnvelope.Serialize();
            }
            else
            {
                return envelope.CloudEvent == null ? string.Empty : envelope.CloudEvent.Serialize();
            }
        }

        private static LogEntryDto GetLogEntry(CloudEventEnvelope envelope, HttpStatusCode statusCode, bool isSuccessStatusCode)
        {
            return new LogEntryDto
            {
                CloudEventId = envelope.CloudEvent?.Id,
                CloudEventType = envelope.CloudEvent?.Type,
                CloudEventResource = envelope.CloudEvent?["resource"]?.ToString(),
                Consumer = envelope.Consumer,
                IsSuccessStatusCode = isSuccessStatusCode,
                Endpoint = envelope.Endpoint,
                SubscriptionId = envelope.SubscriptionId,
                StatusCode = statusCode,
            };
        }
    }
}
