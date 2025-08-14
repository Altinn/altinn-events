using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

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

builder.Services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
builder.Services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
builder.Services.AddSingleton<ICertificateResolverService, CertificateResolverService>();
builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
builder.Services.AddSingleton<RetryBackoffService>();
builder.Services.AddHttpClient<IEventsClient, EventsClient>();
builder.Services.AddHttpClient<IWebhookService, WebhookService>();

builder.Build().Run();
