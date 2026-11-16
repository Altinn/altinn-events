using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Models;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;

public class WebhookServiceCollectionExtensionsTests
{
    private const string _fakeToken = "fake-maskinporten-token";

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public async Task AddWebhookHttpClient_SendsBearerTokenOnlyWhenAuthenticationEnabled(string flag, bool expectToken)
    {
        // Arrange
        Dictionary<string, string> settings = new()
        {
            ["EventsOutboundSettings:MaskinportenSettings:Scope"] = "altinn:system/events.event.push"
        };

        if (flag != null)
        {
            settings["EventsOutboundSettings:EnableWebhookAuthentication"] = flag;
        }

        AuthorizationHeaderCapturingHandler transport = new();

        // Act
        AuthenticationHeaderValue actual = await SendThroughWebhookClient(settings, transport);

        // Assert
        Assert.Equal(expectToken ? _fakeToken : null, actual?.Parameter);
    }

    [Fact]
    public async Task AddWebhookHttpClient_NoEventsOutboundSettings_SendsWithoutToken()
    {
        // Arrange
        AuthorizationHeaderCapturingHandler transport = new();

        // Act
        AuthenticationHeaderValue actual = await SendThroughWebhookClient([], transport);

        // Assert
        Assert.Null(actual);
    }

    private static async Task<AuthenticationHeaderValue> SendThroughWebhookClient(Dictionary<string, string> settings, AuthorizationHeaderCapturingHandler transport)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IMaskinportenService> maskinportenMock = new();
        maskinportenMock
            .Setup(m => m.GetToken(It.IsAny<IClientDefinition>(), It.IsAny<MaskinportenRequestContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new TokenResponse { AccessToken = _fakeToken });

        ServiceCollection services = new();

        // Registered first so the library's TryAdd keeps the fake token service
        services.AddSingleton(maskinportenMock.Object);
        services.AddWebhookHttpClient(config);
        services.AddHttpClient(WebhookService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => transport);

        using ServiceProvider provider = services.BuildServiceProvider();
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(WebhookService.HttpClientName);

        using HttpResponseMessage response = await client.PostAsync("https://subscriber.example.com/hook", new StringContent("{}"));

        return transport.Authorization;
    }

    private sealed class AuthorizationHeaderCapturingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
