using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, s) =>
    {
        var configuration = context.Configuration;
        
        s.AddOptions<PlatformSettings>()
       .Configure<IConfiguration>((settings, config) =>
       {
           config.GetSection("Platform").Bind(settings);
       });
        s.AddOptions<KeyVaultSettings>()
        .Configure<IConfiguration>((settings, configuration) =>
        {
            configuration.GetSection("KeyVault").Bind(settings);
        });
        s.AddOptions<CertificateResolverSettings>()
         .Configure<IConfiguration>((settings, configuration) =>
         {
             configuration.GetSection("CertificateResolver").Bind(settings);
         });

        s.AddApplicationInsightsTelemetryWorkerService();
        s.ConfigureFunctionsApplicationInsights();

        s.AddSingleton<IKeyVaultService, KeyVaultService>();
        s.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
        s.AddSingleton<ICertificateResolverService, CertificateResolverService>();

        s.AddHttpClient<IEventsClient, EventsClient>();
        s.AddHttpClient<IWebhookService, WebhookService>();

        s.AddQueueSenders(configuration);
        s.AddTransient<IRetryBackoffService, RetryBackoffService>();
    }).Build();

// Log after the host is built
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Function host configured successfully");

host.Run();
