#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;

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

        var azureBusConfig = opts.UseAzureServiceBus(azureServiceBusConnectionString);

        if (env.IsDevelopment())
        {
            // Disable Wolverine's internal system queues for the Azure Service Bus Emulator
            // These are temporary queues used for inter-node coordination, leader election, and worker distribution
            // The emulator doesn't support the Management API needed to create these queues dynamically
            // Note: Azure Service Bus native dead-letter queues (accessed via /$deadletterqueue) still work in the emulator
            azureBusConfig.SystemQueuesAreEnabled(false);

            // Auto-purge application queues on startup for clean development sessions
            azureBusConfig.AutoPurgeOnStartup();

            // Development retry policy - shorter delays for faster execution during development and testing
            ConfigureRetryPolicy(
                opts.Policies, 
                cooldownDelays: [100.Milliseconds(), 100.Milliseconds(), 100.Milliseconds()],
                scheduleDelays: [500.Milliseconds(), 500.Milliseconds(), 500.Milliseconds()]);
        }
        else
        {
            // In production, enable auto-provisioning which creates all necessary queues automatically
            // This includes Wolverine's internal system queues for coordination, error handling, retries, and responses
            azureBusConfig.AutoProvision();

            // Production retry policy - longer delays to allow transient issues to resolve, and to avoid overwhelming the system during outages
            ConfigureRetryPolicy(
                opts.Policies,
                cooldownDelays: [1.Seconds(), 5.Seconds(), 10.Seconds()],
                scheduleDelays: [30.Seconds(), 60.Seconds(), 2.Minutes(), 2.Minutes(), 2.Minutes()]);
        }

        return opts;
    }

    /// <summary>
    /// Configures a retry policy with the standard set of retriable exceptions.
    /// Applies the same exception types to both development and production configurations,
    /// ensuring consistent retry behavior across environments while allowing different timing.
    /// </summary>
    /// <param name="policies">The policies configuration.</param>
    /// <param name="cooldownDelays">Delays for cooldown retries (retried within same lock).</param>
    /// <param name="scheduleDelays">Delays for scheduled retries (retried with new locks).</param>
    /// <remarks>
    /// The retry policy handles the following exception types:
    /// <list type="bullet">
    /// <item><description><see cref="ServiceBusException"/>: Azure Service Bus specific errors</description></item>
    /// <item><description><see cref="InvalidOperationException"/>: General Service Bus client errors. 
    /// Note: This is a broad exception type that may also retry non-transient programming errors.</description></item>
    /// <item><description><see cref="TimeoutException"/>: Operation timeouts</description></item>
    /// <item><description><see cref="SocketException"/>: Network connectivity issues</description></item>
    /// <item><description><see cref="TaskCanceledException"/>: Operation cancellations. 
    /// Note: This may delay shutdown when the host CancellationToken is triggered during graceful shutdown.</description></item>
    /// </list>
    /// </remarks>
    private static void ConfigureRetryPolicy(IPolicies policies, TimeSpan[] cooldownDelays, TimeSpan[] scheduleDelays)
    {
        policies
            .OnException<ServiceBusException>()
            .Or<InvalidOperationException>()
            .Or<TimeoutException>()
            .Or<SocketException>()
            .Or<TaskCanceledException>()
            .RetryWithCooldown(cooldownDelays)
            .Then.ScheduleRetry(scheduleDelays)
            .Then.MoveToErrorQueue();
    }
}
