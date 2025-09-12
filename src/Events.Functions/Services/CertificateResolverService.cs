using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Class to resolve certificate to generate an access token
    /// </summary>
    public class CertificateResolverService : ICertificateResolverService
    {
        private readonly ILogger<ICertificateResolverService> _logger;
        private readonly CertificateResolverSettings _certificateResolverSettings;
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private DateTime _reloadTime;
        private X509Certificate2 _cachedX509Certificate = null;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="certificateResolverSettings">Settings for certificate resolver</param>
        /// <param name="keyVaultService">Key vault service</param>
        /// <param name="keyVaultSettings">Key vault settings</param>
        public CertificateResolverService(
            ILogger<ICertificateResolverService> logger,
            IOptions<CertificateResolverSettings> certificateResolverSettings,
            IKeyVaultService keyVaultService,
            IOptions<KeyVaultSettings> keyVaultSettings)
        {
            _logger = logger;
            _certificateResolverSettings = certificateResolverSettings.Value;
            _keyVaultService = keyVaultService;
            _keyVaultSettings = keyVaultSettings.Value;
            _reloadTime = DateTime.MinValue;
        }

        /// <summary>
        /// Find the configured 
        /// </summary>
        /// <returns></returns>
        public async Task<X509Certificate2> GetCertificateAsync()
        {
            if (DateTime.UtcNow > _reloadTime || _cachedX509Certificate == null)
            {
                var certificate = await _keyVaultService.GetCertificateAsync(
                    _keyVaultSettings.KeyVaultURI,
                    _keyVaultSettings.PlatformCertSecretId);
                lock (_lockObject)
                {
                    _cachedX509Certificate = certificate;

                    _reloadTime = DateTime.UtcNow.AddSeconds(_certificateResolverSettings.CacheCertLifetimeInSeconds);
                    _logger.LogInformation("Certificate reloaded.");
                }
            }

            return _cachedX509Certificate;
        }
    }
}
