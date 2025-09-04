using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Configure services first
builder.Services.Configure<PlatformSettings>(builder.Configuration.GetSection("Platform"));
builder.Services.Configure<KeyVaultSettings>(builder.Configuration.GetSection("KeyVault"));
builder.Services.Configure<CertificateResolverSettings>(builder.Configuration.GetSection("CertificateResolver"));
builder.Services.Configure<EventsOutboundSettings>(builder.Configuration.GetSection("EventsOutboundSettings"));

builder.Services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
builder.Services.AddSingleton<ICertificateResolverService, CertificateResolverService>();
builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
builder.Services.AddHttpClient<IEventsClient, EventsClient>();
builder.Services.AddHttpClient<IWebhookService, WebhookService>();

builder.Services.AddQueueSenders(builder.Configuration);
builder.Services.AddTransient<IRetryBackoffService, RetryBackoffService>();

// Build the host
var host = builder.Build();

// Get logger from service provider
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Startup");

logger.LogInformation("Function host configured successfully");

// Start the host
await host.RunAsync();
