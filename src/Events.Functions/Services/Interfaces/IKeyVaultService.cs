using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Services.Interfaces
{
    /// <summary>
    /// Interface for interacting with key vault
    /// </summary>
    public interface IKeyVaultService
    {
        /// <summary>
        /// Gets the certificate from the given key vault.
        /// </summary>
        /// <param name="vaultUri">The URI of the key vault to ask for secret. </param>
        /// <param name="secretId">The id of the secret.</param>
        /// <returns>The certificate as an X509Certificate2</returns>
        Task<X509Certificate2?> GetCertificateAsync(string vaultUri, string secretId);

        /// <summary>
        /// Gets the value of a secret from the given key vault.
        /// </summary>
        /// <param name="vaultUri">The URI of the key vault.</param>
        /// <param name="secretName">The name of the secret.</param>
        /// <returns>The secret value.</returns>
        Task<string> GetSecretAsync(string vaultUri, string secretName);
    }
}
