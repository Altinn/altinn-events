using System.Diagnostics.CodeAnalysis;

using Altinn.Common.AccessTokenClient.Services;

using Altinn.Platform.Events.Functions;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Factories;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(Startup))]

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Function events startup
    /// </summary>
    public class Startup : IWebJobsStartup
    {
        /// <summary>
        /// Gets functions project configuration
        /// </summary>
        /// <remarks>This class is excluded from code coverage because it has no logic to be tested.</remarks>
        [ExcludeFromCodeCoverage]
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddOptions<PlatformSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("Platform").Bind(settings);
                });
            builder.Services.AddOptions<KeyVaultSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("KeyVault").Bind(settings);
                });
            builder.Services.AddOptions<CertificateResolverSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("CertificateResolver").Bind(settings);
                });
            builder.Services.AddOptions<EventsOutboundSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("EventsOutboundSettings").Bind(settings);
                });
            builder.Services.AddSingleton<IQueueProcessorFactory, CustomQueueProcessorFactory>();
            builder.Services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
            builder.Services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
            builder.Services.AddSingleton<ICertificateResolverService, CertificateResolverService>();
            builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
            builder.Services.AddHttpClient<IEventsClient, EventsClient>();
            builder.Services.AddHttpClient<IWebhookService, WebhookService>();
        }
    }
}
