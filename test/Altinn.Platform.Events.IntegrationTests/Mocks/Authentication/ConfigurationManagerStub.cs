using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Events.IntegrationTests.Mocks.Authentication;

/// <summary>
/// Represents a stub of <see cref="ConfigurationManager{OpenIdConnectConfiguration}"/> to be used in integration tests.
/// </summary>
public class ConfigurationManagerStub : IConfigurationManager<OpenIdConnectConfiguration>
{
    /// <inheritdoc />
    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromFile("JWTValidationCert.cer");

        OpenIdConnectConfiguration configuration = new();
        configuration.SigningKeys.Add(new X509SecurityKey(cert));

        return Task.FromResult(configuration);
    }

    /// <inheritdoc />
    public void RequestRefresh()
    {
    }
}
