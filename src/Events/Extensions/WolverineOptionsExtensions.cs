using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Provides extension methods for configuring <see cref="WolverineOptions"/> with default settings.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineOptionsExtensions
{
    /// <summary>
    /// Configures the <see cref="WolverineOptions"/> instance with default settings,
    /// including Azure Service Bus configuration and environment-specific options.
    /// </summary>
    /// <param name="opts">The <see cref="WolverineOptions"/> to configure.</param>
    /// <param name="env">The host environment.</param>
    /// <param name="azureServiceBusConnectionString">The Azure Service Bus connection string.</param>
    /// <returns>The configured <see cref="WolverineOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="opts"/> or <paramref name="env"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="azureServiceBusConnectionString"/> is null or whitespace.</exception>
    public static WolverineOptions ConfigureEventsDefaults(
        this WolverineOptions opts,
        IHostEnvironment env,
        string azureServiceBusConnectionString)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentException.ThrowIfNullOrWhiteSpace(azureServiceBusConnectionString);

        opts.Policies.DisableConventionalLocalRouting();
        opts.EnableAutomaticFailureAcks = false;
        opts.EnableRemoteInvocation = false;
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
        var azureBusConfig = opts
            .UseAzureServiceBus(azureServiceBusConnectionString);

        if (env.IsDevelopment())
        {
            // Disable system queues (dead-letter, error, retries, response queues) for Azure Service Bus Emulator
            // The emulator doesn't support auto-provisioning of dynamic queues
            // Note: This means failed messages won't be moved to error/dead-letter queues in local development
            azureBusConfig.SystemQueuesAreEnabled(false);

            // Auto-purge application queues on startup for clean development sessions
            azureBusConfig.AutoPurgeOnStartup();
        }
        else
        {
            // In production, enable auto-provisioning which creates all necessary queues automatically
            // This includes system queues for error handling, dead-lettering, retries, and responses
            azureBusConfig.AutoProvision();
        }

        return opts;
    }
}
