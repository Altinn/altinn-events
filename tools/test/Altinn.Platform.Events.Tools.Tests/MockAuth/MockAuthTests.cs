using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Events.Tools.MockAuth.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tools.Tests.MockAuth;

public class MockAuthTests(WebApplicationFactory<MockAuthSettings> factory) : IClassFixture<WebApplicationFactory<MockAuthSettings>>
{
    private const string EnterpriseTokenPath = "/api/GetEnterpriseToken?org=ttd&orgNo=991825827&scopes=altinn:events.publish altinn:events.publish.admin";
    private const string PersonalTokenPath = "/api/GetPersonalToken?userId=20734816&partyId=50884402&pid=03827099790&scopes=altinn:events.subscribe";
    private const string PlatformAccessTokenPath = "/api/GetPlatformAccessToken?app=events";

    [Theory]
    [InlineData(EnterpriseTokenPath)]
    [InlineData(PersonalTokenPath)]
    [InlineData(PlatformAccessTokenPath)]
    public async Task TokenEndpoints_WithoutBasicAuth_ReturnUnauthorized(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEnterpriseToken_ValidatesAgainstPublishedJwks_WithOrgAndScopeClaims()
    {
        using var client = AuthenticatedClient();

        var principal = await ValidateBearerAsync(client, await client.GetStringAsync(EnterpriseTokenPath, TestContext.Current.CancellationToken));

        Assert.Equal("ttd", principal.FindFirst("urn:altinn:org")?.Value);
        Assert.Equal("991825827", principal.FindFirst("urn:altinn:orgNumber")?.Value);
        Assert.Equal("altinn:events.publish altinn:events.publish.admin", principal.FindFirst("urn:altinn:scope")?.Value);
    }

    [Fact]
    public async Task GetPersonalToken_ValidatesAgainstPublishedJwks_WithUserClaims()
    {
        using var client = AuthenticatedClient();

        var principal = await ValidateBearerAsync(client, await client.GetStringAsync(PersonalTokenPath, TestContext.Current.CancellationToken));

        Assert.Equal("20734816", principal.FindFirst("urn:altinn:userid")?.Value);
        Assert.Equal("50884402", principal.FindFirst("urn:altinn:partyid")?.Value);
        Assert.Equal("03827099790", principal.FindFirst("pid")?.Value);
        Assert.Equal("altinn:events.subscribe", principal.FindFirst("scope")?.Value);
    }

    [Theory]
    [InlineData("/api/GetPlatformAccessToken?app=events", "platform/events")]
    [InlineData("/api/GetPlatformAccessToken?org=ttd&app=apps-test", "ttd/apps-test")]
    public async Task GetPlatformAccessToken_PassesTheApisAccessTokenHandler_WithTheCertificateInTheVault(string path, string expectedCredential)
    {
        using var client = AuthenticatedClient();
        string token = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        var (succeeded, httpContext) = await HandleAccessTokenAsync(token);

        Assert.True(succeeded);
        Assert.Equal(expectedCredential, httpContext.Items["accesstokencontextid"]);
    }

    [Fact]
    public async Task PlatformAccessToken_SignedWithAnotherKey_IsRejectedByTheApisAccessTokenHandler()
    {
        using var client = AuthenticatedClient();
        string token = await client.GetStringAsync(PlatformAccessTokenPath, TestContext.Current.CancellationToken);
        string forged = token[..token.LastIndexOf('.')] + ".Zm9yZ2Vk";

        var (succeeded, _) = await HandleAccessTokenAsync(forged);

        Assert.False(succeeded);
    }

    [Fact]
    public async Task GetPlatformAccessToken_UnknownIssuer_ReturnsBadRequest()
    {
        using var client = AuthenticatedClient();

        using var response = await client.GetAsync("/api/GetPlatformAccessToken?org=unknown&app=x", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The API's handler from Altinn.Common.AccessToken, with the key provider answering with the public certificate as the app host
    // puts it in the vault: the DER bytes as base64, which PublicSigningKeyProvider decodes.
    private static async Task<(bool Succeeded, HttpContext HttpContext)> HandleAccessTokenAsync(string token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["PlatformAccessToken"] = token;

        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(TestCertificate("platform-org.pfx"), null);
        string vaultSecret = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
        var keyProvider = new Mock<IPublicSigningKeyProvider>();
        keyProvider
            .Setup(p => p.GetSigningKeys(It.IsAny<string>()))
            .ReturnsAsync(() => [new X509SecurityKey(X509CertificateLoader.LoadCertificate(Convert.FromBase64String(vaultSecret)))]);

        var handler = new AccessTokenHandler(
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<AccessTokenHandler>.Instance,
            Options.Create(new AccessTokenSettings()),
            keyProvider.Object);

        var context = new AuthorizationHandlerContext([new AccessTokenRequirement()], new ClaimsPrincipal(), null);
        await handler.HandleAsync(context);
        return (context.HasSucceeded, httpContext);
    }

    private static string TestCertificate(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private HttpClient AuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("mock:mock")));
        return client;
    }

    // The API's validation parameters from Program.cs, with the keys from the JWKS the discovery document points to.
    private static async Task<ClaimsPrincipal> ValidateBearerAsync(HttpClient client, string token)
    {
        string jwks = await client.GetStringAsync("/authentication/api/v1/openid/jwks", TestContext.Current.CancellationToken);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys(),
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }
}
