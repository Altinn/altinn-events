using System.Text;

using Altinn.Platform.Events.Tools.MockAuth;
using Altinn.Platform.Events.Tools.MockAuth.Configuration;

using Microsoft.Extensions.Options;

const string OpenIdPath = "/authentication/api/v1/openid";

// appsettings.json is in the output folder, so it is found whichever folder the issuer is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddUserSecrets<MockAuthSettings>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<MockAuthSettings>(builder.Configuration.GetSection(nameof(MockAuthSettings)));
builder.Services.AddSingleton<MockTokenIssuer>();

var port = builder.Configuration.GetValue("MockAuthSettings:Port", 5081);
builder.WebHost.UseUrls($"http://localhost:{port}");

var app = builder.Build();

// The token endpoints have the same shape as the Altinn test token generator, so the Bruno requests in "Token generator" work unchanged.
app.MapGet("/api/GetEnterpriseToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, string? org, string? orgNo, string? orgNumber, string? scopes) =>
    HasBasicAuth(context, settings.Value)
        ? Results.Text(issuer.CreateEnterpriseToken(BaseUrl(context), org ?? "ttd", orgNo ?? orgNumber ?? "991825827", scopes))
        : Unauthorized(context));

app.MapGet("/api/GetPersonalToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, int? userId, int? partyId, string? pid, int? authLvl, string? scopes) =>
    HasBasicAuth(context, settings.Value)
        ? Results.Text(issuer.CreatePersonalToken(BaseUrl(context), userId ?? 20000000, partyId ?? 50000000, pid ?? "12345678901", authLvl ?? 3, scopes))
        : Unauthorized(context));

// A self-registered user has no national identity number, only an email address.
app.MapGet("/api/GetSelfRegisteredEmailUserToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, int? userId, int? partyId, string? email, string? scopes) =>
    HasBasicAuth(context, settings.Value)
        ? Results.Text(issuer.CreatePersonalToken(BaseUrl(context), userId ?? 20000001, partyId ?? 50000001, null, 0, scopes, email ?? "selfregistered@example.com"))
        : Unauthorized(context));

// The API checks these against the public certificate it looks up in Key Vault by issuer: org=ttd&app=apps-test for an app, app=events for platform.
app.MapGet("/api/GetPlatformAccessToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, string? org, string? app) =>
{
    if (!HasBasicAuth(context, settings.Value))
    {
        return Unauthorized(context);
    }

    string tokenIssuer = org ?? "platform";
    return issuer.CreatePlatformAccessToken(tokenIssuer, app ?? "events") is string token
        ? Results.Text(token)
        : Results.BadRequest($"No access token certificate for issuer '{tokenIssuer}'. Known issuers: {string.Join(", ", MockTokenIssuer.AccessTokenIssuers)}.");
});

app.MapGet($"{OpenIdPath}/.well-known/openid-configuration", (HttpContext context) =>
{
    string baseUrl = BaseUrl(context);
    return Results.Json(new
    {
        issuer = baseUrl,
        jwks_uri = $"{baseUrl}{OpenIdPath}/jwks",
        id_token_signing_alg_values_supported = new[] { "RS256" },
        subject_types_supported = new[] { "public" },
        response_types_supported = new[] { "code" },
    });
});

app.MapGet($"{OpenIdPath}/jwks", (MockTokenIssuer issuer) => Results.Json(issuer.GetJwks()));

app.Run();

static string BaseUrl(HttpContext context) => $"{context.Request.Scheme}://{context.Request.Host}";

static IResult Unauthorized(HttpContext context)
{
    context.Response.Headers.WWWAuthenticate = "Basic";
    return Results.Unauthorized();
}

static bool HasBasicAuth(HttpContext context, MockAuthSettings settings)
{
    string? header = context.Request.Headers.Authorization;
    if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        string credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        return credentials == $"{settings.Username}:{settings.Password}";
    }
    catch (FormatException)
    {
        return false;
    }
}
