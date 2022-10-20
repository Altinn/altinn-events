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
    public class OutboundClient : SecureClientBase, IOutboundClient
    {
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
            : base(httpClient, accessTokenGenerator, keyVaultService, keyVaultSettings)
        {
            var platformSettings = eventsConfig.Value;
            Client.BaseAddress = new Uri(platformSettings.ApiEventsEndpoint);
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task PostOutbound(CloudEvent item)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(item), Encoding.UTF8, "application/json");

            string endpointUrl = "push";

            var accessToken = await GenerateAccessToken("platform", "events");

            HttpResponseMessage response = await Client.PostAsync(endpointUrl, httpContent, accessToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError(
                    $"// Post outbound event with id {item.Id} failed with status code {response.StatusCode}");
                throw new HttpRequestException(
                    $"// Post outbound event with id {item.Id} failed with status code {response.StatusCode}");
            }
        }
    }
}
