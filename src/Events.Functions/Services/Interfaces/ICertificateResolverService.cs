using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Services.Interfaces
{
    /// <summary>
    /// Interface to retrive signing credentials for issuer and signing keys for consumer of tokens
    /// </summary>
    public interface ICertificateResolverService
    {
        /// <summary>
        /// Returns certificate to be used for signing a JWT
        /// </summary>
        /// <returns>The signing credentials</returns>
        public Task<X509Certificate2> GetCertificateAsync();
    }
}
