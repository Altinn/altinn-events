using System;
using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Common.AccessToken.Constants;
using Altinn.Platform.Events.Tests.Mocks;

using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Events.Tests.Utils
{
    public static class PrincipalUtil
    {
        public static readonly string AltinnCoreClaimTypesOrg = "urn:altinn:org";
        public static readonly string AltinnCoreClaimTypesOrgNumber = "urn:altinn:orgNumber";

        public static ClaimsPrincipal GetClaimsPrincipal(string org, string orgNumber, string scope = null)
        {
            string issuer = "www.altinn.no";

            List<Claim> claims = new List<Claim>();
            if (!string.IsNullOrEmpty(org))
            {
                claims.Add(new Claim(AltinnCoreClaimTypesOrg, org, ClaimValueTypes.String, issuer));
            }

            if (scope != null)
            {
                claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
            }

            claims.Add(new Claim(AltinnCoreClaimTypesOrgNumber, orgNumber.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock-org");
            identity.AddClaims(claims);

            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal GetClaimsPrincipal(string orgNumber)
        {
            string issuer = "www.altinn.no";

            List<Claim> claims = new List<Claim>();

            claims.Add(new Claim(AltinnCoreClaimTypesOrgNumber, orgNumber.ToString(), ClaimValueTypes.Integer32, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer));
            claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));

            ClaimsIdentity identity = new ClaimsIdentity("mock-org");
            identity.AddClaims(claims);

            return new ClaimsPrincipal(identity);
        }

        public static ClaimsPrincipal GetClaimsPrincipal(int userId, int authenticationLevel, string scope = null)
        {
            string issuer = "www.altinn.no";

            List<Claim> claims = new List<Claim>
            {
                new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.UserName, "UserOne", ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.PartyID, userId.ToString(), ClaimValueTypes.Integer32, issuer),
                new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer)
            };

            if (scope != null)
            {
                claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
            }

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);

            return new ClaimsPrincipal(identity);
        }

        public static string GetOrgToken(string org, string orgNumber = "991825827", string scope = null)
        {
            ClaimsPrincipal principal = GetClaimsPrincipal(org, orgNumber, scope);

            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

            return token;
        }

        public static string GetToken(int userId, int authenticationLevel = 2, string scope = null)
        {
            ClaimsPrincipal principal = GetClaimsPrincipal(userId, authenticationLevel, scope);

            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5));

            return token;
        }

        public static string GetAccessToken(string issuer, string app)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(AccessTokenClaimTypes.App, app, ClaimValueTypes.String, issuer)
            };

            ClaimsIdentity identity = new ClaimsIdentity("mock");
            identity.AddClaims(claims);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5), issuer);

            return token;
        }

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
    }
}
