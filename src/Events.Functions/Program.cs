using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

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

builder.Build().Run();
