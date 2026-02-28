using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Models.Payloads;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Handles Webhook service
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly HttpClient _client;
        private readonly IEventsClient _eventsClient;
        private readonly ILogger _logger;
        private readonly string _slackUri = "hooks.slack.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        public WebhookService(
            HttpClient client, IEventsClient eventsClient, IOptions<EventsOutboundSettings> eventOutboundSettings, ILogger<WebhookService> logger)
        {
            _client = client;
            _eventsClient = eventsClient;
            _logger = logger;
            _client.Timeout = TimeSpan.FromSeconds(eventOutboundSettings.Value.RequestTimeout);
        }

        /// <inheritdoc/>
        public async Task Send(CloudEventEnvelope envelope)
        {
            string payload = GetPayload(envelope);
            StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _client.PostAsync(envelope.Endpoint, httpContent);

                // log response from webhook to Events
                await _eventsClient.LogWebhookHttpStatusCode(envelope, response.StatusCode, response.IsSuccessStatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync();
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
    }
}
