using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Clients
{
    /// <summary>
    /// Interface to Events API
    /// </summary>
    public class EventsClient : IEventsClient
    {
        private readonly ILogger<IEventsClient> _logger;

        private readonly HttpClient _client;

        private readonly IAccessTokenGenerator _accessTokenGenerator;

        private readonly ICertificateResolverService _certificateResolverService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsClient"/> class.
        /// </summary>
        public EventsClient(
            HttpClient httpClient,
            IAccessTokenGenerator accessTokenGenerator,
            ICertificateResolverService certificateResolverService,
            IOptions<PlatformSettings> eventsConfig,
            ILogger<IEventsClient> logger)
        {
            _client = httpClient;
            _accessTokenGenerator = accessTokenGenerator;
            _certificateResolverService = certificateResolverService;

            var platformSettings = eventsConfig.Value;
            _client.BaseAddress = new Uri(platformSettings.ApiEventsEndpoint);
            _logger = logger;
        }

        /// <summary>
        /// Generate a fresh access token using the client certificate
        /// </summary>
        /// <returns></returns>
        protected async Task<string> GenerateAccessToken()
        {
            X509Certificate2 certificate = await _certificateResolverService.GetCertificateAsync();
            return _accessTokenGenerator.GenerateAccessToken("platform", "events", certificate);
        }

        /// <inheritdoc/>
        public async Task SaveCloudEvent(CloudEvent cloudEvent)
        {
            string endpointUrl = "storage/events";
            var (success, statusCode) = await PostCloudEventToEndpoint(cloudEvent, endpointUrl);

            if (!success)
            {
                var msg = $"// SaveCloudEvent with id {cloudEvent.Id} failed with status code {statusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task PostInbound(CloudEvent cloudEvent)
        {
            string endpointUrl = "inbound";

            var (success, statusCode) = await PostCloudEventToEndpoint(cloudEvent, endpointUrl);

            if (!success)
            {
                var msg = $"// PostInbound event with id {cloudEvent.Id} failed with status code {statusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task PostOutbound(CloudEvent cloudEvent)
        {
            string endpointUrl = "outbound";

            var (success, statusCode) = await PostCloudEventToEndpoint(cloudEvent, endpointUrl);

            if (!success)
            {
                var msg = $"// PostOutbound event with id {cloudEvent.Id} failed with status code {statusCode}";

                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task ValidateSubscription(int subscriptionId)
        {
            var accessToken = await GenerateAccessToken();

            string endpointUrl = "subscriptions/validate/" + subscriptionId;

            HttpResponseMessage response = await _client.PutAsync(endpointUrl, null, accessToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogError("Attempting to validate non existing subscription {subscriptionId}", subscriptionId);
                return;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    $"// Validate subscription with id {subscriptionId} failed with status code {response.StatusCode}");
                throw new HttpRequestException(
                    $"// Validate subscription with id {subscriptionId} failed with status code {response.StatusCode}");
            }
        }

        private async Task<(bool Success, HttpStatusCode StatusCode)> PostCloudEventToEndpoint(CloudEvent cloudEvent, string endpoint)
        {
            StringContent httpContent = new(cloudEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json");

            var accessToken = await GenerateAccessToken();

            HttpResponseMessage response = await _client.PostAsync(endpoint, httpContent, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, response.StatusCode);
            }

            return (true, response.StatusCode);
        }
    }
}
