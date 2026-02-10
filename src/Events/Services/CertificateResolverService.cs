using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service for resolving X509 certificates from certificate store
    /// </summary>
    public class CertificateResolverService : ICertificateResolverService
    {
        private readonly PlatformSettings _platformSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateResolverService"/> class.
        /// </summary>
        public CertificateResolverService(IOptions<PlatformSettings> platformSettings)
        {
            _platformSettings = platformSettings.Value;
        }

        /// <inheritdoc/>
        public Task<X509Certificate2> GetCertificateAsync()
        {
            // Get certificate thumbprint from configuration
            string thumbprint = _platformSettings.CertificateThumbprint;
            
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new InvalidOperationException("Certificate thumbprint is not configured");
            }

            // Open the certificate store
            using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            // Find the certificate by thumbprint
            X509Certificate2Collection certificates = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: false);

            if (certificates.Count == 0)
            {
                throw new InvalidOperationException($"Certificate with thumbprint '{thumbprint}' not found");
            }

            return Task.FromResult(certificates[0]);
        }
    }
}
