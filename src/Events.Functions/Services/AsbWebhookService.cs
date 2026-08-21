using System.Text;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models.Payloads;
using Altinn.Platform.Events.Functions.Wolverine.Models;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Sends outbound and subscription-validation webhook calls for the ASB-triggered Wolverine
    /// listeners. Behaves like the Events API's own <c>WebhookService</c>, but logs the webhook
    /// attempt via an authenticated callback to the Events API (<see cref="IEventsClient"/>)
    /// instead of a direct database write, since this app has no database access. Named
    /// distinctly from the existing <see cref="WebhookService"/>, which stays in place for the
    /// legacy Storage-Queue-triggered functions.
    /// </summary>
    public class AsbWebhookService : IWebhookService
    {
        private readonly HttpClient _client;
        private readonly IEventsClient _eventsClient;
        private readonly ILogger<AsbWebhookService> _logger;
        private readonly string _slackUri = "hooks.slack.com";

        /// <summary>Name of the named <see cref="HttpClient"/> used by this service.</summary>
        internal const string _httpClientName = nameof(AsbWebhookService);

        /// <summary>
        /// Initializes a new instance of the <see cref="AsbWebhookService"/> class. Takes
        /// <see cref="IHttpClientFactory"/> (not a typed HttpClient) and is registered via a plain
        /// AddScoped, matching the Events API's own WebhookService — Wolverine's handler code
        /// generation rejects services registered through AddHttpClient's typed-client overload,
        /// since that's an opaque factory registration its ServiceLocationPolicy won't allow.
        /// </summary>
        public AsbWebhookService(
            IHttpClientFactory httpClientFactory, IEventsClient eventsClient, IOptions<EventsOutboundSettings> eventOutboundSettings, ILogger<AsbWebhookService> logger)
        {
            _client = httpClientFactory.CreateClient(_httpClientName);
            _eventsClient = eventsClient;
            _logger = logger;
            _client.Timeout = TimeSpan.FromSeconds(eventOutboundSettings.Value.RequestTimeout);
        }

        /// <inheritdoc/>
        public async Task Send(CloudEventEnvelope envelope, CancellationToken cancellationToken)
        {
            string payload = GetPayload(envelope);
            using StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                using HttpResponseMessage response = await _client.PostAsync(envelope.Endpoint, httpContent, cancellationToken);

                // log response from webhook back to Events, mirroring the existing Storage-Queue path's IEventsClient callback
                await _eventsClient.LogWebhookHttpStatusCode(ToFunctionsEnvelope(envelope), response.StatusCode, response.IsSuccessStatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("AsbWebhookService send failed to send cloud event id {CloudEventId} {SubscriptionId} {Reason} {Response}", envelope.CloudEvent?.Id, envelope.SubscriptionId, reason, response);

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

        private static Models.CloudEventEnvelope ToFunctionsEnvelope(CloudEventEnvelope envelope)
        {
            return new Models.CloudEventEnvelope
            {
                CloudEvent = envelope.CloudEvent,
                Pushed = envelope.Pushed,
                Endpoint = envelope.Endpoint,
                Consumer = envelope.Consumer,
                SubscriptionId = envelope.SubscriptionId
            };
        }
    }
}
