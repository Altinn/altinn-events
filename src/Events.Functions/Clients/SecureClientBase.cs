using System;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Clients;

/// <summary>
/// Base class responsible for generating access tokens and retrieving client certificates.
/// </summary>
public class SecureClientBase
{
    /// <summary>
    /// HttpClient provided via dependency injection.
    /// </summary>
    protected HttpClient Client { get; }

    /// <summary>
    /// Use to generate access tokens
    /// </summary>
    protected IAccessTokenGenerator AccessTokenGenerator { get; }

    /// <summary>
    /// Access to KeyVault
    /// </summary>
    protected IKeyVaultService KeyVaultService { get; }

    /// <summary>
    /// KeyVault settings
    /// </summary>
    protected KeyVaultSettings KeyVaultSettings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureClientBase"/> class.
    /// </summary>
    public SecureClientBase(
        HttpClient client, 
        IAccessTokenGenerator accessTokenGenerator, 
        IKeyVaultService keyVaultService,
        IOptions<KeyVaultSettings> keyVaultSettings)
    {
        Client = client;
        AccessTokenGenerator = accessTokenGenerator;
        KeyVaultService = keyVaultService;
        KeyVaultSettings = keyVaultSettings.Value;
    }

    /// <summary>
    /// Generate a fresh access token using the client certificate
    /// </summary>
    /// <returns></returns>
    protected async Task<string> GenerateAccessToken(string issuer, string app)
    {
        string certBase64 =
            await KeyVaultService.GetCertificateAsync(
                KeyVaultSettings.KeyVaultURI,
                KeyVaultSettings.PlatformCertSecretId);
        string accessToken = AccessTokenGenerator.GenerateAccessToken(issuer, app, new X509Certificate2(
            Convert.FromBase64String(certBase64),
            (string)null,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable));
        return accessToken;
    }
}