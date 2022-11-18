using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
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

        private readonly IKeyVaultService _keyVaultService;

        private readonly KeyVaultSettings _keyVaultSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsClient"/> class.
        /// </summary>
        public EventsClient(
            HttpClient httpClient,
            IAccessTokenGenerator accessTokenGenerator,
            IKeyVaultService keyVaultService,
            IOptions<PlatformSettings> eventsConfig,
            IOptions<KeyVaultSettings> keyVaultSettings,
            ILogger<IEventsClient> logger)
        {
            _client = httpClient;
            _accessTokenGenerator = accessTokenGenerator;
            _keyVaultService = keyVaultService;
            _keyVaultSettings = keyVaultSettings.Value;

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
            string certBase64 =
                await _keyVaultService.GetCertificateAsync(
                    _keyVaultSettings.KeyVaultURI,
                    _keyVaultSettings.PlatformCertSecretId);
            string accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "events", new X509Certificate2(
                Convert.FromBase64String(certBase64),
                (string)null,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable));
            return accessToken;
        }

        /// <inheritdoc/>
        public async Task SaveCloudEvent(CloudEvent cloudEvent)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");

            var accessToken = await GenerateAccessToken();

            string endpointUrl = "storage/events";

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, httpContent, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                var msg = $"// SaveCloudEvent with id {cloudEvent.Id} failed with status code {response.StatusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task PostInbound(CloudEvent cloudEvent)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");

            string endpointUrl = "inbound";

            var accessToken = await GenerateAccessToken();

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, httpContent, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                var msg = $"// PostInbound with cloudEvent Id {cloudEvent.Id} failed, status code: {response.StatusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task PostOutbound(CloudEvent cloudEvent)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");

            string endpointUrl = "outbound";

            var accessToken = await GenerateAccessToken();

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, httpContent, accessToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError(
                    $"// Post outbound event with id {cloudEvent.Id} failed with status code {response.StatusCode}");
                throw new HttpRequestException(
                    $"// Post outbound event with id {cloudEvent.Id} failed with status code {response.StatusCode}");
            }
        }

        /// <inheritdoc/>
        public async Task ValidateSubscription(int subscriptionId)
        {
            var accessToken = await GenerateAccessToken();

            string endpointUrl = "subscriptions/validate/" + subscriptionId;

            HttpResponseMessage response = await _client.PutAsync(endpointUrl, null, accessToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError(
                    $"// Validate subscription with id {subscriptionId} failed with statuscode {response.StatusCode}");
                throw new HttpRequestException(
                    $"// Validate subscription with id {subscriptionId} failed with statuscode {response.StatusCode}");
            }
        }
    }
}
