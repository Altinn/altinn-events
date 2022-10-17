using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Clients
{
    /// <summary>
    /// Service to validate subscription
    /// </summary>
    public class ValidateSubscriptionClient : SecureClientBase, IValidateSubscriptionClient
    {
        private readonly HttpClient _client;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private readonly ILogger<IOutboundClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateSubscriptionClient"/> class.
        /// </summary>
        public ValidateSubscriptionClient(
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
        public async Task ValidateSubscription(int subscriptionId)
        {
            try
            {
                string endpointUrl = "subscriptions/validate/" + subscriptionId;

                var accessToken = await GenerateAccessToken("platform", "events");

                HttpResponseMessage response = await _client.PutAsync(endpointUrl, null, accessToken);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError($"// Validate subscription with id {subscriptionId} failed with statuscode {response.StatusCode}");
                    throw new HttpRequestException($"// Validate subscription with id {subscriptionId} failed with statuscode {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"// Validate subscription with id {subscriptionId} failed with errormessage {e.Message}");
                throw;
            }
        }
    }
}
