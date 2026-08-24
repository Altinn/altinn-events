using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Wolverine.Extensions;
using Altinn.Platform.Events.Functions.Wolverine.Policies;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Platform.Events.Functions.Extensions;

/// <summary>
/// Provides extension methods for registering Wolverine and its Azure Service Bus wiring
/// for the ASB-triggered outbound and subscription-validation listeners. Mirrors the
/// equivalent extension in the main Events API project.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine, and registers the flag-driven outbound and subscription-validation
    /// Azure Service Bus listeners.
    /// </summary>
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        FunctionsWolverineSettings wolverineSettings = config.GetSection("WolverineSettings").Get<FunctionsWolverineSettings>()
            ?? throw new InvalidOperationException("Configuration section 'WolverineSettings' is missing entirely.");

        services.AddWolverine(opts =>
        {
            if (wolverineSettings.IsAzureServiceBusEnabled)
            {
                opts.ConfigureEventsDefaults(env, wolverineSettings.ServiceBusConnectionString!);

                AddOutboundListener(wolverineSettings, opts);
                AddValidationListener(wolverineSettings, opts);
            }
        });

        // Split registration (named client + plain AddScoped), not AddHttpClient<TClient,TImpl>'s typed-client
        // overload — Wolverine's handler code generation rejects that as an opaque factory registration.
        // Matches Events' own WebhookService registration for the identical reason.
        services.AddHttpClient(AsbWebhookService._httpClientName);
        services.AddScoped<IWebhookService, AsbWebhookService>();
    }

    private static void AddOutboundListener(FunctionsWolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableOutboundListener)
        {
            return;
        }

        opts.ListenToAzureServiceBusQueue(settings.OutboundQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.Policies.Add(new OutboundEventHandlerPolicy(settings));
    }

    private static void AddValidationListener(FunctionsWolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableValidationListener)
        {
            return;
        }

        opts.ListenToAzureServiceBusQueue(settings.ValidationQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.Policies.Add(new ValidationEventHandlerPolicy(settings));
    }
}
