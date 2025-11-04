using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.BridgeProxy
{
    /// <summary>
    /// Extension methods for registering BridgeProxy services in the dependency injection container.
    /// </summary>
    public static class BridgeProxyServiceCollectionExtensions
    {
        /// <summary>
        /// Adds BridgeProxy services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="config">The application configuration.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddBridgeProxy(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<BridgeProxyOptions>(config.GetSection("BridgeProxy"));

            services.AddHttpClient<BridgeForwarder>((sp, client) =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BridgeProxyOptions>>().Value;
                if (string.IsNullOrWhiteSpace(opts.BaseAddress))
                {
                    throw new InvalidOperationException("BridgeProxy:BaseAddress must be configured.");
                }

                client.BaseAddress = new Uri(opts.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });

            services.AddTransient<BridgeProxyEndpoint>();
            return services;
        }
    }
}
