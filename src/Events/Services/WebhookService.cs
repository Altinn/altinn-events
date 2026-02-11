using System;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service for sending HTTP requests to webhook endpoints
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly HttpClient _httpClient;
        private readonly ITraceLogService _traceLogService;
        private readonly ILogger<WebhookService> _logger;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly ICertificateResolverService _certificateResolverService;
        private readonly PlatformSettings _platformSettings;
        private const string _slackUri = "hooks.slack.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        public WebhookService(
            HttpClient httpClient,
            ILogger<WebhookService> logger,
            IAccessTokenGenerator accessTokenGenerator,
            ICertificateResolverService certificateResolverService,
            IOptions<PlatformSettings> platformSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _accessTokenGenerator = accessTokenGenerator;
            _certificateResolverService = certificateResolverService;
            _platformSettings = platformSettings.Value;
            _httpClient.Timeout = TimeSpan.FromSeconds(300); // 5 minute timeout
            _httpClient.BaseAddress = new Uri(_platformSettings.ApiEventsEndpoint);
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
                HttpResponseMessage response = await _httpClient.PostAsync(envelope.Endpoint, httpContent);

                // Log response from webhook to Events
                await LogWebhookHttpStatusCode(envelope, response.StatusCode, response.IsSuccessStatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "WebhookService send failed to send cloud event id {CloudEventId} {SubscriptionId} {Reason} {Response}", 
                        envelope.CloudEvent?.Id, 
                        envelope.SubscriptionId, 
                        reason, 
                        response);

                    throw new HttpRequestException(reason);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Send to webhook with {SubscriptionId} failed with error message {Message}", envelope.SubscriptionId, e.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task Send(CloudEventEnvelope envelope, CancellationToken cancellationToken)
        {
            string payload = GetPayload(envelope);
            using StringContent httpContent = new(payload, Encoding.UTF8, "application/json");

            try
            {
                using HttpResponseMessage response = await _httpClient.PostAsync(envelope.Endpoint, httpContent, cancellationToken);

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

        /// <summary>
        /// Generate a fresh access token using the client certificate
        /// </summary>
        private async Task<string> GenerateAccessToken()
        {
            X509Certificate2 certificate = await _certificateResolverService.GetCertificateAsync();
            return _accessTokenGenerator.GenerateAccessToken("platform", "events", certificate);
        }

        /// <summary>
        /// Log response from webhook post to subscriber.
        /// </summary>
        /// <param name="cloudEventEnvelope">Wrapper object for cloud event and subscriber data</param>
        /// <param name="statusCode">Http status code returned</param>
        /// <param name="isSuccessStatusCode">Boolean value that indicates whether the status code was successful or not</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task LogWebhookHttpStatusCode(
            CloudEventEnvelope cloudEventEnvelope, 
            HttpStatusCode statusCode, 
            bool isSuccessStatusCode)
        {
            try
            {
                var endpoint = "storage/events/logs";

                var logEntryData = new LogEntryDto
                {
                    CloudEventId = cloudEventEnvelope.CloudEvent?.Id,
                    CloudEventType = cloudEventEnvelope.CloudEvent?.Type,
                    CloudEventResource = cloudEventEnvelope.CloudEvent?["resource"]?.ToString(),
                    Consumer = cloudEventEnvelope.Consumer,
                    IsSuccessStatusCode = isSuccessStatusCode,
                    Endpoint = cloudEventEnvelope.Endpoint,
                    SubscriptionId = cloudEventEnvelope.SubscriptionId,
                    StatusCode = statusCode,
                };

                StringContent httpContent = new(
                    JsonSerializer.Serialize(logEntryData), 
                    Encoding.UTF8, 
                    "application/json");

                var accessToken = await GenerateAccessToken();
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Failed to log webhook status code for cloud event id {CloudEventId}. Status: {StatusCode}",
                        cloudEventEnvelope.CloudEvent?.Id,
                        response.StatusCode);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e, 
                    "Failed to log trace log webhook status code for cloud event id {CloudEventId}", 
                    cloudEventEnvelope.CloudEvent?.Id);
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
