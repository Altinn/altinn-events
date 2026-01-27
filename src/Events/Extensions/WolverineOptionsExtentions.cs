using System;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Provides extension methods for configuring <see cref="WolverineOptions"/> with default settings.
/// </summary>
public static class WolverineOptionsExtentions
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
            azureBusConfig.SystemQueuesAreEnabled(false);
            azureBusConfig.AutoPurgeOnStartup();
        }
        else
        {
            azureBusConfig.AutoProvision();
        }

        return opts;
    }
}
