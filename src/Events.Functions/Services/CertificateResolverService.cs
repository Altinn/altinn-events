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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Events.Functions.Services
{
    /// <summary>
    /// Class to resolve certificate to sign JWT token uses as Access token
    /// </summary>
    public class CertificateResolverService : ICertificateResolverService
    {
        private readonly IKeyVaultService _keyVaultService;
        private readonly KeyVaultSettings _keyVaultSettings;
        private static X509Certificate2 _cachedX509Certificate = null;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="keyVaultService">Key vault service</param>
        /// <param name="keyVaultSettings">Key vault settings</param>
        public CertificateResolverService(
            IKeyVaultService keyVaultService,
            IOptions<KeyVaultSettings> keyVaultSettings)
        {
            _keyVaultService = keyVaultService;
            _keyVaultSettings = keyVaultSettings.Value;
        }

        /// <summary>
        /// Find the configured 
        /// </summary>
        /// <returns></returns>
        public async Task<X509Certificate2> GetCertificateAsync()
        {
            string certBase64 = await _keyVaultService.GetCertificateAsync(
                    _keyVaultSettings.KeyVaultURI,
                    _keyVaultSettings.PlatformCertSecretId);

            return new X509Certificate2(
                Convert.FromBase64String(certBase64),
                (string)null,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }
    }
}
