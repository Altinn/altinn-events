using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Models.Payloads;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;

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
        public WebhookService(HttpClient client, ILogger<WebhookService> logger)
        {
            _client = client;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task Send(CloudEventEnvelope envelope)
        {
            string payload = GetPayload(envelope);
            StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _client.PostAsync(envelope.Endpoint, httpContent);

                // todo: post to storage controller with status code
                await PostWebhookResponseForLogging(envelope.CloudEvent, response.StatusCode);

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
        /// Posts the provided cloud event to the logging endpoint
        /// </summary>
        /// <param name="cloudEvent">The cloud event associated with the logging entry <see cref="CloudEvent"/></param>
        /// <param name="statusCode">The status code returned from the webhook endpoint</param>
        /// <returns></returns>
        internal async Task PostWebhookResponseForLogging(CloudEvent cloudEvent, HttpStatusCode statusCode)
        {
            await _client.PostAsync("/api/storage/events/logs", new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json"));
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
