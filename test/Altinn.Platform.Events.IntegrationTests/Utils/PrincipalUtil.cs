using System;
using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Platform.Events.IntegrationTests.Mocks;

using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Events.IntegrationTests.Utils;

/// <summary>
/// Utility class for creating ClaimsPrincipal and generating tokens for testing purposes.
/// </summary>
public static class PrincipalUtil
{
    /// <summary>
    /// Generates a token for an organization based on organization name, organization number, and optional scope.
    /// </summary>
    public static string GetOrgToken(string org, string orgNumber = "991825827", string scope = null)
    {
        ClaimsPrincipal principal = GetClaimsPrincipal(org, orgNumber, scope);

        return JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
    }

    /// <summary>
    /// Generates a ClaimsPrincipal for an organization based on organization name, organization number, optional scope.
    /// </summary>
    public static ClaimsPrincipal GetClaimsPrincipal(string org, string orgNumber, string scope = null)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber, ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
        ];

        if (scope != null)
        {
            claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        if (!string.IsNullOrEmpty(org))
        {
            claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
        }

        ClaimsIdentity identity = new("mock-org");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }
}
