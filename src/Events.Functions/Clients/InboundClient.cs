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
    /// Client used to send cloudEvents to events-inbound queue.
    /// </summary>
    public class InboundClient : SecureClientBase, IInboundClient
    {
        private readonly ILogger<IInboundClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboundClient"/> class.
        /// </summary>
        public InboundClient(
            HttpClient httpClient,
            IAccessTokenGenerator accessTokenGenerator,
            IKeyVaultService keyVaultService,
            IOptions<PlatformSettings> eventsConfig,
            IOptions<KeyVaultSettings> keyVaultSettings,
            ILogger<IInboundClient> logger)
            : base(httpClient, accessTokenGenerator, keyVaultService, keyVaultSettings)
        {
            var platformSettings = eventsConfig.Value;
            Client.BaseAddress = new Uri(platformSettings.ApiEventsEndpoint);
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task PostInbound(CloudEvent item)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(item), Encoding.UTF8, "application/json");

            string endpointUrl = "inbound";

            var accessToken = await GenerateAccessToken("platform", "events");

            HttpResponseMessage response = await Client.PutAsync(endpointUrl, httpContent, accessToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var msg = $"// PostInbound with cloudEvent Id {item.Id} failed, status code: {response.StatusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }
    }
}
