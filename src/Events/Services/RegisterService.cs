using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Register.Models;

using AltinnCore.Authentication.Utils;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Handles register service
    /// </summary>
    public class RegisterService : IRegisterService
    {
        private readonly HttpClient _client;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly GeneralSettings _generalSettings;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly ILogger<IRegisterService> _logger;

        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegisterService"/> class.
        /// </summary>
        public RegisterService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IAccessTokenGenerator accessTokenGenerator,
            IOptions<GeneralSettings> generalSettings,
            IOptions<PlatformSettings> platformSettings,
            ILogger<IRegisterService> logger)
        {
            _client = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _generalSettings = generalSettings.Value;
            _accessTokenGenerator = accessTokenGenerator;
            _logger = logger;

            _client.BaseAddress = new Uri(platformSettings.Value.RegisterApiBaseAddress);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _serializerOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <inheritdoc/>
        public async Task<int> PartyLookup(string orgNo, string person)
        {
            string endpointUrl = "v1/parties/lookup";

            PartyLookup partyLookup = new PartyLookup() { Ssn = person, OrgNo = orgNo };

            string bearerToken = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _generalSettings.JwtCookieName);
            string accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "events");

            StringContent content = new StringContent(JsonSerializer.Serialize(partyLookup));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            HttpResponseMessage response = await _client.PostAsync(bearerToken, endpointUrl, content, accessToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Party party = await response.Content.ReadFromJsonAsync<Party>(_serializerOptions);
                return party.PartyId;
            }
            else
            {
                string reason = await response.Content.ReadAsStringAsync();
                _logger.LogError("// RegisterService // PartyLookup // Failed to lookup party in platform register. Response {response}. \n Reason {reason}.", response, reason);

                throw await PlatformHttpException.CreateAsync(response);
            }
        }

        /// <inheritdoc/>
        public async Task<List<PartyIdentifiers>> PartyLookup(List<string> partyUrnList, int chunkSize = 100)
        {
            string bearerToken = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _generalSettings.JwtCookieName);
            string accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "events");
            
            List<PartyIdentifiers> partyIdentifiers = [];

            // The Register API only supports 100 entries per request
            foreach (string[] chunk in partyUrnList.Chunk(chunkSize))
            {
                PartiesRegisterQueryRequest partyLookup = new() { Data = chunk };

                StringContent content = new(JsonSerializer.Serialize(partyLookup));
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                const string RequestUri = "v2/internal/parties/query?fields=identifiers";
                HttpResponseMessage response = await _client.PostAsync(bearerToken, RequestUri, content, accessToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    PartiesRegisterQueryResponse queryResults = 
                        await response.Content.ReadFromJsonAsync<PartiesRegisterQueryResponse>(_serializerOptions);

                    partyIdentifiers.AddRange(queryResults.Data);
                }
                else
                {
                    throw await PlatformHttpException.CreateAsync(response);
                }
            }

            return partyIdentifiers;
        }
    }
}
