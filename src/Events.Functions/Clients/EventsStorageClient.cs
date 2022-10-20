using System;
using System.Net;
using System.Net.Http;
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
    public class EventsStorageClient : SecureClientBase, IEventsStorageClient
    {
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
            : base(httpClient, accessTokenGenerator, keyVaultService, keyVaultSettings)
        {
            var platformSettings = eventsConfig.Value;
            Client.BaseAddress = new Uri(platformSettings.ApiEventsEndpoint);
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task SaveCloudEvent(CloudEvent cloudEvent)
        {
            StringContent httpContent = new(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");

            var accessToken = await GenerateAccessToken("platform", "events");

            string endpointUrl = "storage/events";

            HttpResponseMessage response = await Client.PostAsync(endpointUrl, httpContent, accessToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var msg = $"// SaveCloudEvent with id {cloudEvent.Id} failed with status code {response.StatusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }
    }
}
