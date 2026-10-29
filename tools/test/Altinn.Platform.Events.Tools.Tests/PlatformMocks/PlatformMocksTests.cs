using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Interfaces;
using Altinn.Common.PEP.Implementation;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tools.PlatformMocks;
using Altinn.Platform.Events.Tools.PlatformMocks.Configuration;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tools.Tests.PlatformMocks;

public sealed class PlatformMocksTests : IAsyncLifetime
{
    private const string CertificatePassword = "test";
    private const string Resource = "urn:altinn:resource:ttd-altinn-events-automated-tests";

    private readonly string _certificatePath = Path.Combine(Path.GetTempPath(), $"platform-mocks-test-{Guid.NewGuid()}.pfx");
    private readonly PlatformMocksSettings _settings;
    private readonly PlatformMocksService _service;

    public PlatformMocksTests()
    {
        _settings = new PlatformMocksSettings
        {
            Register = new RegisterMockSettings
            {
                Port = FreePort(),
                KnownParties = [new KnownParty { PartyId = 51326197, OrganizationNumber = "311443755" }],
                MainUnits = new() { ["311443755"] = "314249879" },
            },
            Authorization = new AuthorizationMockSettings { Port = FreePort(), DeniedSubjects = ["1337"] },
            Webhooks = new WebhooksMockSettings { Port = FreePort(), CertificatePath = _certificatePath, CertificatePassword = CertificatePassword, SlowSeconds = 1 },
        };

        _service = new PlatformMocksService(Options.Create(_settings), NullLogger<PlatformMocksService>.Instance);
    }

    public async ValueTask InitializeAsync()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        await File.WriteAllBytesAsync(_certificatePath, certificate.Export(X509ContentType.Pfx, CertificatePassword));

        await _service.StartAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.StopAsync(CancellationToken.None);
        File.Delete(_certificatePath);
    }

    [Fact]
    public async Task Register_PartyLookupByUrn_ThroughTheApisClient_FindsKnownAndOtherParties()
    {
        var client = RegisterClient();

        var parties = (await client.PartyLookup(
            ["urn:altinn:party:id:51326197", "urn:altinn:organization:identifier-no:310000001", "urn:altinn:person:identifier-no:07837399275"],
            TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(3, parties.Count);
        Assert.Equal(("organization", "311443755"), (parties[0].PartyType, parties[0].OrganizationIdentifier));
        Assert.Equal(RegisterMockSettings.OrganizationPartyIdOffset + 310000001, parties[1].PartyId);
        Assert.Equal(("person", "07837399275"), (parties[2].PartyType, parties[2].PersonIdentifier));

        // An organisation's party id leads back to it.
        var byPartyId = (await client.PartyLookup([$"urn:altinn:party:id:{parties[1].PartyId}"], TestContext.Current.CancellationToken)).Single();
        Assert.Equal("310000001", byPartyId.OrganizationIdentifier);
    }

    [Fact]
    public async Task Register_PartyLookupByOrganizationNumber_ThroughTheApisClient_ReturnsItsPartyId()
    {
        int partyId = await RegisterClient().PartyLookup("311443755", null);

        Assert.Equal(51326197, partyId);
    }

    [Theory]
    [InlineData("311443755", "314249879")]
    [InlineData("314249879", null)]
    public async Task Register_GetMainUnit_ThroughTheApisClient_ReturnsTheConfiguredMainUnit(string organizationNumber, string? expectedMainUnit)
    {
        var mainUnit = await RegisterClient().GetMainUnit("urn:altinn:organization:identifier-no:" + organizationNumber, TestContext.Current.CancellationToken);

        Assert.Equal(expectedMainUnit, mainUnit?.OrganizationIdentifier);
    }

    [Fact]
    public async Task Authorization_MultipleConsumers_ThroughTheApisAuthorizationService_PermitsAllButDeniedSubjects()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHttpContextAccessor()
            .Configure<Altinn.Common.PEP.Configuration.PlatformSettings>(s => s.ApiAuthorizationEndpoint = $"http://localhost:{_settings.Authorization.Port}/authorization/api/v1/")
            .AddSingleton<IPDP, PDPAppSI>();
        services.AddHttpClient<AuthorizationApiClient>();
        using var provider = services.BuildServiceProvider();

        var authorization = new AuthorizationService(
            provider.GetRequiredService<IPDP>(),
            Mock.Of<IClaimsPrincipalProvider>(),
            Mock.Of<IRegisterService>(),
            NullLogger<AuthorizationService>.Instance);

        var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "automated.test.created",
            Source = new Uri("https://example.com/test"),
            Subject = "urn:altinn:organization:identifier-no:311443755",
        };
        cloudEvent["resource"] = Resource;

        var results = await authorization.AuthorizeMultipleConsumersForGenericEvent(cloudEvent, ["/org/ttd", "/user/1337", "/user/20000000"], TestContext.Current.CancellationToken);

        Assert.Equal(new Dictionary<string, bool> { ["/org/ttd"] = true, ["/user/1337"] = false, ["/user/20000000"] = true }, results);
    }

    [Theory]
    [InlineData("ok/a", "automated.test.created", HttpStatusCode.OK)]
    [InlineData("ok/a", "platform.events.validatesubscription", HttpStatusCode.OK)]
    [InlineData("anything", "automated.test.created", HttpStatusCode.OK)]
    [InlineData("fail/a", "automated.test.created", HttpStatusCode.ServiceUnavailable)]
    [InlineData("unvalidated/a", "platform.events.validatesubscription", HttpStatusCode.BadRequest)]
    [InlineData("unvalidated/a", "automated.test.created", HttpStatusCode.OK)]
    [InlineData("slow/a", "automated.test.created", HttpStatusCode.OK)]
    public async Task Webhooks_AnswerByScenario_OverHttps(string path, string type, HttpStatusCode expected)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.Subject == "CN=localhost" };
        using var client = new HttpClient(handler);

        using var response = await client.PostAsync(
            $"https://localhost:{_settings.Webhooks.Port}/webhooks/{path}",
            new StringContent($$"""{"specversion":"1.0","id":"1","type":"{{type}}","source":"https://example.com"}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    private RegisterApiClient RegisterClient()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());

        var accessTokenGenerator = new Mock<IAccessTokenGenerator>();
        accessTokenGenerator.Setup(g => g.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>())).Returns("access-token");

        return new RegisterApiClient(
            httpClientFactory.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            accessTokenGenerator.Object,
            Options.Create(new GeneralSettings { JwtCookieName = "AltinnStudioRuntime" }),
            Options.Create(new PlatformSettings { RegisterApiBaseAddress = $"http://localhost:{_settings.Register.Port}/register/api/" }),
            NullLogger<RegisterApiClient>.Instance);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
