using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;

using Altinn.AccessManagement.Core.Models;
using Altinn.Common.AccessToken.Constants;
using Altinn.Platform.Events.Tests.Mocks;

using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Events.Tests.Utils;

/// <summary>
/// Utility class for creating ClaimsPrincipal and generating tokens for testing purposes.
/// </summary>
public static class PrincipalUtil
{
    /// <summary>
    /// Generates an access token for a given issuer and app.
    /// </summary>
    /// <param name="issuer">The issuer of the token.</param>
    /// <param name="app">The app for which the token is generated.</param>
    /// <returns>A JWT access token.</returns>
    public static string GetAccessToken(string issuer, string app)
    {
        List<Claim> claims =
        [
            new Claim(AccessTokenClaimTypes.App, app, ClaimValueTypes.String, issuer)
        ];

        ClaimsIdentity identity = new("mock");
        identity.AddClaims(claims);

        ClaimsPrincipal principal = new(identity);

        return JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5), issuer);
    }

    /// <summary>
    /// Generates a ClaimsPrincipal for an organization based on the organization number.
    /// </summary>
    /// <param name="orgNumber">The organization number.</param>
    /// <returns>A ClaimsPrincipal representing the organization.</returns>
    public static ClaimsPrincipal GetClaimsPrincipal(string orgNumber)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber, ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer),
        ];

        ClaimsIdentity identity = new("mock-org");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Generates a ClaimsPrincipal for a user based on user ID, authentication level, and optional scope.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="authenticationLevel">The authentication level.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A ClaimsPrincipal representing the user.</returns>
    public static ClaimsPrincipal GetClaimsPrincipal(int userId, int authenticationLevel, string scope = null)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.UserName, "UserOne", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.PartyID, userId.ToString(), ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer)
        ];

        if (scope != null)
        {
            claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        ClaimsIdentity identity = new("mock");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Generates a ClaimsPrincipal for an organization based on organization name, organization number, optional scope, and optional authentication method.
    /// </summary>
    /// <param name="org">The organization name.</param>
    /// <param name="orgNumber">The organization number.</param>
    /// <param name="scope">The optional scope.</param>
    /// <param name="authenticationMethod">The optional authentication method.</param>
    /// <returns>A ClaimsPrincipal representing the organization.</returns>
    public static ClaimsPrincipal GetClaimsPrincipal(string org, string orgNumber, string scope = null, string authenticationMethod = null)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber, ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, authenticationMethod ?? "Mock", ClaimValueTypes.String, issuer),
        ];

        if (scope != null)
        {
            claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        if (!string.IsNullOrEmpty(org))
        {
            claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
        }

        ClaimsIdentity identity = new(authenticationMethod ?? "mock-org");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Generates an expired token for testing purposes.
    /// </summary>
    /// <returns>An expired JWT token.</returns>
    public static string GetExpiredToken()
    {
        return "eyJhbGciOiJSUzI1NiIsImtpZCI6IjQ4Q0VFNjAzMzEwMkYzMjQzMTk2NDc4QUYwNkZCNDNBMTc2NEQ4NDMiLCJ4NXQiOiJTTTdtQXpFQzh5UXhsa2"
               + "VLOEctME9oZGsyRU0iLCJ0eXAiOiJKV1QifQ.eyJ1cm46YWx0aW5uOnVzZXJpZCI6IjEiLCJ1cm46YWx0aW5uOnVzZXJuYW1lIjoiVXNlck9uZSI"
               + "sInVybjphbHRpbm46cGFydHlpZCI6MSwidXJuOmFsdGlubjphdXRoZW50aWNhdGVtZXRob2QiOiJNb2NrIiwidXJuOmFsdGlubjphdXRobGV2ZWw"
               + "iOjIsIm5iZiI6MTU4ODc2NjU4NSwiZXhwIjoxNTg4NzY2NTg5LCJpYXQiOjE1ODg3NjY1ODUsImF1ZCI6ImFsdGlubi5ubyJ9.JbwlNTwnCpafFZ"
               + "-452MM9ZvxTdkb2pMbPFsIOEqB0rvj62Xtex44fHW1Sf2mIld7UmEgw8Lfg-8qKz1SBXx-CYk_ZF-1g7suldEHqhjovQ8IQUwFMy8JaPbVJrZigI"
               + "2UI2myHAETj67YnKkAdImvEUPrJXYtRAOYzp4jH_GrFkGXe30sx3RIvCt2k5BFdsV2Q6kLXsH4Q0jpfJR0XkG1mhgojPc8es1no8XWB8yn8HZGgi"
               + "I9d4F2Edrs0nhCKs5JSFbdX5jVNAhYOw833yNxzSI5keFlRrCN_BXDSqdo9bn8joCwnCJ9fZ3kv_ieYKbMa0tgcN9lBM_KcGQU5EPxpA";
    }

    /// <summary>
    /// Generates a token for an organization based on organization name, organization number, and optional scope.
    /// </summary>
    /// <param name="org">The organization name.</param>
    /// <param name="orgNumber">The organization number. Default is "991825827".</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A JWT token for the organization.</returns>
    public static string GetOrgToken(string org, string orgNumber = "991825827", string scope = null)
    {
        ClaimsPrincipal principal = GetClaimsPrincipal(org, orgNumber, scope);

        return JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));
    }

    /// <summary>
    /// Generates a token for a user based on user ID, authentication level, and optional scope.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="authenticationLevel">The authentication level. Default is 2.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A JWT token for the user.</returns>
    public static string GetToken(int userId, int authenticationLevel = 2, string scope = null)
    {
        ClaimsPrincipal principal = GetClaimsPrincipal(userId, authenticationLevel, scope);

        return JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5));
    }

    /// <summary>
    /// Generates a token for a system-user.
    /// </summary>
    /// <param name="systemId">The system identifier.</param>
    /// <param name="systemUserId">The system user identifier.</param>
    /// <param name="orgClaimId">The org claim identifier.</param>
    /// <param name="authenticationLevel">The authentication level.</param>
    /// <returns>A JWT token for the system-user.</returns>
    public static string GetTokenForSystemUser(string systemId, string systemUserId, string orgClaimId, int authenticationLevel = 3)
    {
        ClaimsPrincipal principal = GetSystemUserPrincipal(systemId, systemUserId, orgClaimId, authenticationLevel);

        return JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5));
    }

    /// <summary>
    /// Creates a ClaimsPrincipal object representing a system user.
    /// </summary>
    /// <param name="systemId">The system identifier.</param>
    /// <param name="systemUserId">The system user identifier.</param>
    /// <param name="ownerIdentifier">The organization number of the system owner.</param>
    /// <param name="authenticationLevel">The authentication level.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> object representing the system user.</returns>
    public static ClaimsPrincipal GetSystemUserPrincipal(string systemId, string systemUserId, string ownerIdentifier, int authenticationLevel)
    {
        string issuer = "www.altinn.no";

        var systemUserClaim = new SystemUserClaim
        {
            System_id = systemId,
            Systemuser_id = [systemUserId],
            Systemuser_org = new OrgClaim
            {
                ID = ownerIdentifier
            }
        };

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
            new Claim("authorization_details", JsonSerializer.Serialize(systemUserClaim), ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, Convert.ToString(authenticationLevel), ClaimValueTypes.Integer32, issuer),
        ];

        var identity = new ClaimsIdentity(claims, "mock");

        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }
}
