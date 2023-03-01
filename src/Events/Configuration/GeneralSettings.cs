namespace Altinn.Platform.Events.Configuration
{
    /// <summary>
    /// Configuration object used to hold general settings for the events application.
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// Base Uri
        /// </summary>
        public string BaseUri { get; set; }

        /// <summary>
        /// Open Id Connect Well known endpoint
        /// </summary>
        public string OpenIdWellKnownEndpoint { get; set; }

        /// <summary>
        /// Name of the cookie for where JWT is stored
        /// </summary>
        public string JwtCookieName { get; set; }

        /// <summary>
        /// Boolean indicating whether support for external events is supported or not
        /// </summary>
        public bool EnableExternalEvents { get; set; }
    }
}
