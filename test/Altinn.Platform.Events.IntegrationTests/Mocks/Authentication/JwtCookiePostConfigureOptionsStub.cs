using System;
using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Altinn.Platform.Events.IntegrationTests.Mocks.Authentication;

/// <summary>
/// Represents a stub for the <see cref="JwtCookiePostConfigureOptions"/> class to be used in integration tests.
/// </summary>
public class JwtCookiePostConfigureOptionsStub : IPostConfigureOptions<JwtCookieOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string name, JwtCookieOptions options)
    {
        if (string.IsNullOrEmpty(options.JwtCookieName))
        {
            options.JwtCookieName = JwtCookieDefaults.CookiePrefix + name;
        }

        options.CookieManager ??= new ChunkingCookieManager();

        if (!string.IsNullOrEmpty(options.MetadataAddress) && !options.MetadataAddress.EndsWith('/'))
        {
            options.MetadataAddress += "/";
        }

        if (!string.IsNullOrEmpty(options.MetadataAddress) &&
            !options.MetadataAddress.EndsWith(".well-known/openid-configuration", StringComparison.Ordinal))
        {
            options.MetadataAddress += ".well-known/openid-configuration";
        }
        options.ConfigurationManager = new ConfigurationManagerStub();
    }
}
