using System.Diagnostics.CodeAnalysis;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Service configuration for Events library
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configures Events services for dependency injection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        [ExcludeFromCodeCoverage]
        public static IServiceCollection AddEventsServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<PlatformSettings>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("Platform").Bind(settings);
                });
            services.AddOptions<KeyVaultSettings>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("KeyVault").Bind(settings);
                });
            services.AddOptions<CertificateResolverSettings>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("CertificateResolver").Bind(settings);
                });
            services.AddOptions<EventsOutboundSettings>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("EventsOutboundSettings").Bind(settings);
                });
            
            services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
            services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
            services.AddSingleton<ICertificateResolverService, CertificateResolverService>();
            services.AddSingleton<IKeyVaultService, KeyVaultService>();
            
            return services;
        }
    }
}
