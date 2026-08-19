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

                AddRegistrationPublisher(wolverineSettings, opts);
                AddInboundPublisher(wolverineSettings, opts);
                AddOutboundPublisher(wolverineSettings, opts);
                AddValidationPublisher(wolverineSettings, opts);

                AddRegistrationListener(wolverineSettings, opts);
                AddInboundListener(wolverineSettings, opts);
                AddOutboundListener(wolverineSettings, opts);
                AddValidationListener(wolverineSettings, opts);
            }
        });

        RegisterRegistrationEventPublisher(services, wolverineSettings);
        RegisterSubscriptionValidationPublisher(services, wolverineSettings);
    }

    private static void AddRegistrationPublisher(WolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableRegistrationPublisher)
        {
            return;
        }

        opts.PublishMessage<RegisterEventCommand>()
            .ToAzureServiceBusQueue(settings.RegistrationQueueName)
            .SendInline();
    }

    private static void AddInboundPublisher(WolverineSettings settings, WolverineOptions opts)
    {
        opts.PublishMessage<InboundEventCommand>()
            .ToAzureServiceBusQueue(settings.InboundQueueName)
            .SendInline();
    }

    private static void AddOutboundPublisher(WolverineSettings settings, WolverineOptions opts)
    {
        opts.PublishMessage<OutboundEventCommand>()
            .ToAzureServiceBusQueue(settings.OutboundQueueName)
            .SendInline();
    }

    private static void AddValidationPublisher(WolverineSettings settings, WolverineOptions opts)
    {
        if (!settings.EnableValidationPublisher)
        {
            return;
        }

        opts.PublishMessage<ValidateSubscriptionCommand>()
            .ToAzureServiceBusQueue(settings.ValidationQueueName)
            .SendInline();
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

    /// <summary>
    /// Registers the ASB or legacy Storage Queue implementation of <see cref="IRegistrationEventPublisher"/>,
    /// depending on <see cref="WolverineSettings.EnableRegistrationPublisher"/>. Internal (rather than private)
    /// so it can be tested directly without going through <see cref="AddWolverineServices"/> and its Azure
    /// Service Bus transport setup.
    /// </summary>
    internal static void RegisterRegistrationEventPublisher(IServiceCollection services, WolverineSettings settings)
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

    /// <summary>
    /// Registers the ASB or legacy Storage Queue implementation of <see cref="ISubscriptionValidationPublisher"/>,
    /// depending on <see cref="WolverineSettings.EnableValidationPublisher"/>. Internal (rather than private)
    /// so it can be tested directly without going through <see cref="AddWolverineServices"/> and its Azure
    /// Service Bus transport setup.
    /// </summary>
    internal static void RegisterSubscriptionValidationPublisher(IServiceCollection services, WolverineSettings settings)
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
