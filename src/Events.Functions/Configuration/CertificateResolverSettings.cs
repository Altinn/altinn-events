namespace Altinn.Platform.Events.Functions.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for the KeyVault.
    /// </summary>
    public class CertificateResolverSettings
    {
        /// <summary>
        /// Uri to keyvault
        /// </summary>
        public int CacheCertLifetimeInSeconds { get; set; } = 3600;
    }
}
