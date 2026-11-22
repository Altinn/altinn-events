using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

using Altinn.Platform.Events.Functions.Services.Interfaces;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Wrapper implementation for a KeyVaultClient. The wrapped client is created with a principal obtained through configuration.
    /// </summary>
    /// <remarks>This class is excluded from code coverage because it has no logic to be tested.</remarks>
    [ExcludeFromCodeCoverage]
    public class KeyVaultService : IKeyVaultService
    {
        /// <inheritdoc/>
        public async Task<X509Certificate2?> GetCertificateAsync(string vaultUri, string secretId)
        {
            CertificateClient certificateClient = new(new Uri(vaultUri), new DefaultAzureCredential());
            AsyncPageable<CertificateProperties> certificatePropertiesPage = certificateClient.GetPropertiesOfCertificateVersionsAsync(secretId);
            await foreach (CertificateProperties certificateProperties in certificatePropertiesPage)
            {
                if (certificateProperties.Enabled == true &&
                    (certificateProperties.ExpiresOn == null || certificateProperties.ExpiresOn >= DateTime.UtcNow))
                {
                    X509Certificate2 cert = await certificateClient.DownloadCertificateAsync(certificateProperties.Name, certificateProperties.Version);
                    return cert;
                }
            }

            return null;
        }
    }
}
