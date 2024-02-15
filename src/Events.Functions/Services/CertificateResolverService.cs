using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Altinn.Common.AccessTokenClient.Configuration;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Class to resolve certificate to sign JWT token uses as Access token
    /// </summary>
    public class CertificateResolverService : ICertificateResolverService
    {
        private readonly ILogger<ICertificateResolverService> _logger;
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private DateTime _expiryTime;
        private static X509Certificate2 _cachedX509Certificate = null;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Default constructor
        /// </summary>
       /// <param name="logger">The logger</param>
        /// <param name="keyVaultService">Key vault service</param>
        /// <param name="keyVaultSettings">Key vault settings</param>
        public CertificateResolverService(
            ILogger<ICertificateResolverService> logger,
            IKeyVaultService keyVaultService,
            IOptions<KeyVaultSettings> keyVaultSettings)
        {
            _logger = logger;
            _keyVaultService = keyVaultService;
            _keyVaultSettings = keyVaultSettings.Value;
            _expiryTime = DateTime.MinValue;
        }

        /// <summary>
        /// Find the configured 
        /// </summary>
        /// <returns></returns>
        public async Task<X509Certificate2> GetCertificateAsync()
        {
            if (DateTime.UtcNow > _expiryTime || _cachedX509Certificate == null)
            {
                string certBase64 = await _keyVaultService.GetCertificateAsync(
                    _keyVaultSettings.KeyVaultURI,
                    _keyVaultSettings.PlatformCertSecretId);

                lock (_lockObject)
                {
                    _cachedX509Certificate = new X509Certificate2(
                        Convert.FromBase64String(certBase64),
                        (string)null,
                        X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                }

                _expiryTime = DateTime.UtcNow.AddHours(1); // Set the expiry time to one hour from now
                _logger.LogInformation("Generated new access token.");
            }

            return _cachedX509Certificate;
        }
    }
}
