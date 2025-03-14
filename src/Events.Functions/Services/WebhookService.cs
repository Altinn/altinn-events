using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
        private readonly ILogger _logger;
        private readonly string _slackUri = "hooks.slack.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        public WebhookService(
            HttpClient client, IOptions<EventsOutboundSettings> eventOutboundSettings, ILogger<WebhookService> logger)
        {
            _client = client;
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
                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"// WebhookService // Send // Failed to send cloud event id {envelope.CloudEvent.Id}, subscriptionId: {envelope.SubscriptionId}. \nReason: {reason} \nResponse: {response}");

                    throw new HttpRequestException(reason);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"// Send to webhook with subscriptionId: {envelope.SubscriptionId} failed with error message {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prepares the provided cloud envelope as serialized payload
        /// </summary>
        internal string GetPayload(CloudEventEnvelope envelope)
        {
            if (envelope.Endpoint.OriginalString.Contains(_slackUri))
            {
                SlackEnvelope slackEnvelope = new()
                {
                    CloudEvent = envelope.CloudEvent
                };
                return slackEnvelope.Serialize();
            }
            else
            {
                return envelope.CloudEvent.Serialize();
            }
        }
    }
}
