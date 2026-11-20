using System.Net;
using System.Net.Http.Headers;

using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Models;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingExtensions;

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
            ["EventsWebhookClient:MaskinportenSettings:Scope"] = "altinn:system/events.event.push",
            ["EventsWebhookClient:MaskinportenSettings:EncodedJwk"] = "fake-jwk"
        };

        if (flag != null)
        {
            settings["EventsOutboundSettings:EnableWebhookAuthentication"] = flag;
        }

        // Act
        AuthenticationHeaderValue actual = await SendThroughWebhookClient(settings);

        // Assert
        Assert.Equal(expectToken ? _fakeToken : null, actual?.Parameter);
    }

    [Fact]
    public void AddWebhookHttpClient_AuthenticationEnabledWithoutJwk_Throws()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["EventsOutboundSettings:EnableWebhookAuthentication"] = "true" })
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddWebhookHttpClient(config));
    }

    [Fact]
    public async Task AddWebhookHttpClient_NoEventsOutboundSettings_SendsWithoutToken()
    {
        // Act
        AuthenticationHeaderValue actual = await SendThroughWebhookClient([]);

        // Assert
        Assert.Null(actual);
    }

    private static async Task<AuthenticationHeaderValue> SendThroughWebhookClient(Dictionary<string, string> settings)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        AuthorizationHeaderCapturingHandler transport = new();

        Mock<IMaskinportenService> maskinportenMock = new();
        maskinportenMock
            .Setup(m => m.GetToken(It.IsAny<IClientDefinition>(), It.IsAny<MaskinportenRequestContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new TokenResponse { AccessToken = _fakeToken });

        ServiceCollection services = new();

        // Registered first so the library's TryAdd keeps the fake token service
        services.AddSingleton(maskinportenMock.Object);
        services.AddWebhookHttpClient(config);
        services.AddHttpClient(AsbWebhookService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => transport);

        using ServiceProvider provider = services.BuildServiceProvider();
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(AsbWebhookService.HttpClientName);

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
