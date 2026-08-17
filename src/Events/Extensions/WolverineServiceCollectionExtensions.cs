using System.Diagnostics.CodeAnalysis;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Commands;
using Altinn.Platform.Events.Wolverine.Policies;
using Altinn.Platform.Events.Wolverine.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Provides extension methods for registering Wolverine and its Azure Service Bus wiring.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine, and registers the flag-driven Azure Service Bus vs. legacy Storage Queue
    /// publishers and listeners for each queue.
    /// </summary>
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        WolverineSettings wolverineSettings = config.GetSection("WolverineSettings").Get<WolverineSettings>() ?? new WolverineSettings();

        services.AddWolverine(opts =>
        {
            if (wolverineSettings.IsAzureServiceBusEnabled)
            {
                opts.ConfigureEventsDefaults(env, wolverineSettings.ServiceBusConnectionString);

                opts.PublishMessage<RegisterEventCommand>()
                    .ToAzureServiceBusQueue(wolverineSettings.RegistrationQueueName);
                opts.PublishMessage<InboundEventCommand>()
                    .ToAzureServiceBusQueue(wolverineSettings.InboundQueueName);
                opts.PublishMessage<OutboundEventCommand>()
                    .ToAzureServiceBusQueue(wolverineSettings.OutboundQueueName);
                opts.PublishMessage<ValidateSubscriptionCommand>()
                    .ToAzureServiceBusQueue(wolverineSettings.ValidationQueueName);

                AddRegistrationListener(wolverineSettings, opts);
                AddInboundListener(wolverineSettings, opts);
                AddOutboundListener(wolverineSettings, opts);
                AddValidationListener(wolverineSettings, opts);
            }

            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());
        });

        RegisterRegistrationEventPublisher(services, wolverineSettings);
        RegisterSubscriptionValidationPublisher(services, wolverineSettings);
    }

    private static void AddRegistrationListener(WolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableRegistrationListener)
        {
            return;
        }

        opts.ListenToAzureServiceBusQueue(settings.RegistrationQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.Policies.Add(new RegistrationEventHandlerPolicy(settings));
    }

    private static void AddInboundListener(WolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableInboundListener)
        {
            return;
        }

        opts.ListenToAzureServiceBusQueue(settings.InboundQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.Policies.Add(new InboundEventHandlerPolicy(settings));
    }

    private static void AddOutboundListener(WolverineSettings settings, WolverineOptions opts)
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

    private static void AddValidationListener(WolverineSettings settings, WolverineOptions opts)
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

    private static void RegisterRegistrationEventPublisher(IServiceCollection services, WolverineSettings settings)
    {
        if (settings.EnableRegistrationPublisher)
        {
            services.AddScoped<IRegistrationEventPublisher, RegistrationEventPublisher>();
        }
        else
        {
            services.AddScoped<IRegistrationEventPublisher, StorageQueueRegistrationEventPublisher>();
        }
    }

    private static void RegisterSubscriptionValidationPublisher(IServiceCollection services, WolverineSettings settings)
    {
        if (settings.EnableValidationPublisher)
        {
            services.AddScoped<ISubscriptionValidationPublisher, SubscriptionValidationPublisher>();
        }
        else
        {
            services.AddScoped<ISubscriptionValidationPublisher, StorageQueueSubscriptionValidationPublisher>();
        }
    }
}
