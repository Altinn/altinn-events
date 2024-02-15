using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Services.Interfaces
{
    /// <summary>
    /// Interface to retrive certificate for access token
    /// </summary>
    public interface ICertificateResolverService
    {
        /// <summary>
        /// Returns certificate to be used for signing an access token
        /// </summary>
        /// <returns>The signing credentials</returns>
        public Task<X509Certificate2> GetCertificateAsync();
    }
}
