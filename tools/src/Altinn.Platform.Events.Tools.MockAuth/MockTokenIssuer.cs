using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Events.Tools.MockAuth;

/// <summary>
/// Issues Altinn-style tokens signed with the API tests' certificates. Bearer tokens are signed with the self-signed certificate
/// whose public key the OpenID endpoints publish. Platform access tokens are signed with the org certificates whose public part
/// the app host puts in the local Key Vault, where the API looks it up by issuer.
/// </summary>
internal sealed class MockTokenIssuer
{
    internal const string CertificateFile = "jwtselfsignedcert.pfx";
    private const string CertificatePassword = "qwer1234";
    private const string ClaimIssuer = "www.altinn.no";

    // Issuer of a platform access token -> the certificate it is signed with. The API tests use the same certificate for both.
    private static readonly Dictionary<string, string> _accessTokenCertificates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["platform"] = "platform-org.pfx",
        ["ttd"] = "ttd-org.pfx",
    };

    private readonly X509SecurityKey _key;
    private readonly SigningCredentials _signingCredentials;
    private readonly Dictionary<string, SigningCredentials> _accessTokenSigningCredentials;

    public MockTokenIssuer()
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, CertificateFile), CertificatePassword);
        _key = new X509SecurityKey(certificate);
        _signingCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);

        _accessTokenSigningCredentials = _accessTokenCertificates.ToDictionary(
            c => c.Key,
            c => (SigningCredentials)new X509SigningCredentials(X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, c.Value), null), SecurityAlgorithms.RsaSha256),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The key the bearer tokens are signed with, for validating them in tests.
    /// </summary>
    internal SecurityKey SigningKey => _key;

    /// <summary>
    /// The issuers there is a platform access token certificate for.
    /// </summary>
    internal static IEnumerable<string> AccessTokenIssuers => _accessTokenCertificates.Keys;

    /// <summary>
    /// Creates a service owner token with the same claims as <c>PrincipalUtil.GetOrgToken</c> in the API tests.
    /// </summary>
    public string CreateEnterpriseToken(string issuer, string org, string orgNumber, string? scopes)
    {
        var identity = new ClaimsIdentity("mock-org");
        identity.AddClaim(new Claim("urn:altinn:org", org, ClaimValueTypes.String, ClaimIssuer));
        if (NormalizeScopes(scopes) is string scope)
        {
            identity.AddClaim(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        identity.AddClaim(new Claim("urn:altinn:orgNumber", orgNumber, ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authlevel", "3", ClaimValueTypes.Integer32, ClaimIssuer));

        return CreateToken(identity, issuer, _signingCredentials);
    }

    /// <summary>
    /// Creates an end user token with the same claims as <c>PrincipalUtil.GetUserToken</c> in the API tests, plus the
    /// <c>pid</c> and <c>scope</c> claims a token from ID-porten carries, like the Altinn test token generator's personal token.
    /// </summary>
    public string CreatePersonalToken(string issuer, int userId, int partyId, string? pid, int authenticationLevel, string? scopes, string? email = null)
    {
        var identity = new ClaimsIdentity("mock");
        identity.AddClaim(new Claim("urn:altinn:userid", userId.ToString(), ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:username", email ?? "UserOne", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:partyid", partyId.ToString(), ClaimValueTypes.Integer32, ClaimIssuer));
        if (pid is not null)
        {
            identity.AddClaim(new Claim("pid", pid, ClaimValueTypes.String, ClaimIssuer));
        }

        if (email is not null)
        {
            identity.AddClaim(new Claim("email", email, ClaimValueTypes.String, ClaimIssuer));
        }

        identity.AddClaim(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authlevel", authenticationLevel.ToString(), ClaimValueTypes.Integer32, ClaimIssuer));
        if (NormalizeScopes(scopes) is string scope)
        {
            identity.AddClaim(new Claim("scope", scope, ClaimValueTypes.String, ClaimIssuer));
        }

        return CreateToken(identity, issuer, _signingCredentials);
    }

    /// <summary>
    /// Creates a platform access token like <c>AccessTokenGenerator.GenerateAccessToken(issuer, app)</c>: the issuer is the
    /// org (or <c>platform</c>) and the app is in <c>urn:altinn:app</c>. Null when there is no certificate for the issuer.
    /// </summary>
    public string? CreatePlatformAccessToken(string issuer, string app)
    {
        if (!_accessTokenSigningCredentials.TryGetValue(issuer, out var signingCredentials))
        {
            return null;
        }

        var identity = new ClaimsIdentity("AccessToken");
        identity.AddClaim(new Claim("urn:altinn:app", app, ClaimValueTypes.String, issuer));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = issuer,
            NotBefore = DateTime.UtcNow.AddSeconds(-5),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = signingCredentials,
        });
    }

    /// <summary>
    /// The test token generator takes scopes separated by commas or spaces. A token carries them separated by spaces.
    /// </summary>
    internal static string? NormalizeScopes(string? scopes)
    {
        string[] parts = (scopes ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>
    /// The public signing key as a JSON Web Key Set.
    /// </summary>
    public object GetJwks()
    {
        var jwk = JsonWebKeyConverter.ConvertFromX509SecurityKey(_key, representAsRsaKey: true);
        return new
        {
            keys = new[]
            {
                new { kty = jwk.Kty, use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = jwk.Kid, n = jwk.N, e = jwk.E },
            },
        };
    }

    private static string CreateToken(ClaimsIdentity identity, string issuer, SigningCredentials signingCredentials)
    {
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = issuer,
            Audience = "altinn.no",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = signingCredentials,
        });
    }
}
