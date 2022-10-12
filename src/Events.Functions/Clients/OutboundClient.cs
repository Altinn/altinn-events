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
    /// Handles PushEvents service
    /// </summary>
    public class OutboundClient : IOutboundClient
    {
        private readonly HttpClient _client;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private readonly ILogger<IOutboundClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundClient"/> class.
        /// </summary>
        public OutboundClient(
            HttpClient httpClient,
            IAccessTokenGenerator accessTokenGenerator,
            IKeyVaultService keyVaultService,
            IOptions<PlatformSettings> eventsConfig,
            IOptions<KeyVaultSettings> keyVaultSettings,
            ILogger<IOutboundClient> logger)
        {
            var platformSettings = eventsConfig.Value;
            httpClient.BaseAddress = new Uri(platformSettings.ApiEventsEndpoint);
            _keyVaultSettings = keyVaultSettings.Value;
            _client = httpClient;
            _accessTokenGenerator = accessTokenGenerator;
            _keyVaultService = keyVaultService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task PostOutbound(CloudEvent item)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(item), Encoding.UTF8, "application/json");
            try
            {
                string endpointUrl = "push";

                string certBase64 = await _keyVaultService.GetCertificateAsync(_keyVaultSettings.KeyVaultURI, _keyVaultSettings.PlatformCertSecretId);
                string accessToken = _accessTokenGenerator.GenerateAccessToken(
                    "platform",
                    "events",
                    new X509Certificate2(Convert.FromBase64String(certBase64), (string)null, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable));

                HttpResponseMessage response = await _client.PostAsync(endpointUrl, httpContent, accessToken);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError($"// Post outbound event with id {item.Id} failed with statuscode {response.StatusCode}");
                    throw new HttpRequestException($"// Post outbound event with id {item.Id} failed with statuscode {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"// Post outbound event with id {item.Id} failed with errormessage {e.Message}");
                throw;
            }
        }
    }
}
