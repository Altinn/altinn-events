namespace Altinn.Platform.Events.Functions.Configuration
{
    /// <summary>
    /// Configuration object used to hold settings for the CertificateResolver.
    /// </summary>
    public class CertificateResolverSettings
    {
        /// <summary>
        /// Certificatee cache life time
        /// </summary>
        public int CacheCertLifetimeInSeconds { get; set; } = 3600;
    }
}
