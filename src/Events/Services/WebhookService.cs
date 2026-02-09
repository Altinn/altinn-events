using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service for sending HTTP requests to webhook endpoints
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebhookService> _logger;
        private const string SlackUri = "hooks.slack.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        public WebhookService(HttpClient httpClient, ILogger<WebhookService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(300); // 5 minute timeout
        }

        /// <inheritdoc/>
        public async Task SendAsync(CloudEventEnvelope envelope)
        {
            if (envelope?.Endpoint == null)
            {
                throw new ArgumentNullException(nameof(envelope), "Envelope or Endpoint cannot be null");
            }

            string payload = GetPayload(envelope);
            StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation(
                    "Sending webhook request to {Endpoint} for subscription {SubscriptionId}",
                    envelope.Endpoint,
                    envelope.SubscriptionId);

                HttpResponseMessage response = await _httpClient.PostAsync(envelope.Endpoint, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Webhook request failed for subscription {SubscriptionId}. Status: {StatusCode}, Reason: {Reason}",
                        envelope.SubscriptionId,
                        response.StatusCode,
                        reason);

                    throw new HttpRequestException(
                        $"Webhook request failed with status code {response.StatusCode}. Reason: {reason}");
                }

                _logger.LogInformation(
                    "Successfully sent webhook request to {Endpoint} for subscription {SubscriptionId}",
                    envelope.Endpoint,
                    envelope.SubscriptionId);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send webhook request to {Endpoint} for subscription {SubscriptionId}",
                    envelope.Endpoint,
                    envelope.SubscriptionId);
                throw;
            }
        }

        /// <summary>
        /// Prepares the payload for the webhook request.
        /// For Slack endpoints, wraps the cloud event in a Slack-compatible format.
        /// For other endpoints, sends the cloud event directly.
        /// </summary>
        private string GetPayload(CloudEventEnvelope envelope)
        {
            if (envelope.Endpoint?.OriginalString?.Contains(SlackUri, StringComparison.OrdinalIgnoreCase) == true)
            {
                // Wrap in Slack format
                var slackPayload = new
                {
                    text = envelope.CloudEvent?.Serialize() ?? string.Empty
                };
                return System.Text.Json.JsonSerializer.Serialize(slackPayload);
            }

            return envelope.CloudEvent?.Serialize() ?? string.Empty;
        }
    }
}
