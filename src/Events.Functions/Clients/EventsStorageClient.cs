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
    /// Events Storage REST API client
    /// </summary>
    public class EventsStorageClient : IEventsStorageClient
    {
        private readonly HttpClient _client;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private readonly PlatformSettings _platformSettings;
        private readonly ILogger<IOutboundClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsStorageClient"/> class.
        /// </summary>
        public EventsStorageClient(
            HttpClient httpClient,
            IAccessTokenGenerator accessTokenGenerator,
            IKeyVaultService keyVaultService,
            IOptions<PlatformSettings> eventsConfig,
            IOptions<KeyVaultSettings> keyVaultSettings,
            ILogger<IOutboundClient> logger)
        {
            _platformSettings = eventsConfig.Value;
            _keyVaultSettings = keyVaultSettings.Value;
            httpClient.BaseAddress = new Uri(_platformSettings.ApiEventsEndpoint);
            _client = httpClient;
            _accessTokenGenerator = accessTokenGenerator;
            _keyVaultService = keyVaultService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task SaveCloudEvent(CloudEvent item)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(item), Encoding.UTF8, "application/json");
            try
            {
                string endpointUrl = "storage/events";

                string certBase64 = await _keyVaultService.GetCertificateAsync(_keyVaultSettings.KeyVaultURI, _keyVaultSettings.PlatformCertSecretId);
                string accessToken = _accessTokenGenerator.GenerateAccessToken(
                    "platform",
                    "events",
                    new X509Certificate2(Convert.FromBase64String(certBase64), (string)null, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable));

                HttpResponseMessage response = await _client.PostAsync(endpointUrl, httpContent, accessToken);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = $"// SaveCloudEvent with id {item.Id} failed with status code {response.StatusCode}";
                    _logger.LogError(msg);
                    throw new HttpRequestException(msg);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"// SaveCloudEvent with id {item.Id} failed with error message {e.Message}");
                throw;
            }
        }
    }
}
