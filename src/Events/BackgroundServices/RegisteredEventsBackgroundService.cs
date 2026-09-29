using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.BackgroundServices;

/// <summary>
/// Polls for registered events and forwards them to outbound delivery, replacing the
/// events-inbound Azure Service Bus queue. Runs <see cref="EventsProcessingSettings.TaskCount"/>
/// concurrent polling loops, each resolving a scoped <see cref="IRegisteredEventsProcessingService"/>
/// per iteration.
/// </summary>
/// <remarks>
/// Only the first (primary) task polls unconditionally. Additional tasks stay idle until the
/// primary task observes a sustained backlog (<see cref="EventsProcessingSettings.RampUpLimit"/>
/// consecutive successful claims), at which point they start polling too. This avoids running
/// <see cref="EventsProcessingSettings.TaskCount"/> concurrent pollers at all times when the
/// actual event volume is low.
/// </remarks>
public class RegisteredEventsBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EventsProcessingSettings> settings,
    ILogger<RegisteredEventsBackgroundService> logger) : BackgroundService
{
    private readonly EventsProcessingSettings _settings = settings.Value;
    private readonly ManyEventsLately _manyEventsLately = new();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.TaskCount <= 0)
        {
            // No tasks configured: processing is effectively disabled. Useful for tests
            // that don't want the background service running.
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Task[] tasks = Enumerable.Range(0, _settings.TaskCount)
                    .Select(i => RunProcessingLoop(isFirstInstance: i == 0, stoppingToken))
                    .ToArray();

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }

                logger.LogError(e, "// RegisteredEventsBackgroundService // ExecuteAsync // Unhandled error. Restarting processing loops after delay.");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }

    private async Task RunProcessingLoop(bool isFirstInstance, CancellationToken stoppingToken)
    {
        TimeSpan idleDelay = TimeSpan.FromSeconds(isFirstInstance
            ? _settings.PrimaryTaskIdleDelaySeconds
            : _settings.AdditionalTasksIdleDelaySeconds);

        int consecutiveClaims = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool shouldIdle = await RunSingleIterationAsync(isFirstInstance, stoppingToken, ref consecutiveClaims);

                if (shouldIdle && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "// RegisteredEventsBackgroundService // RunProcessingLoop // Unhandled error.");

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
        }
    }

    /// <summary>
    /// Runs a single polling iteration: either idles (if this is a non-primary task waiting
    /// for ramp-up) or attempts to claim and process one event, updating the ramp-up state
    /// as needed.
    /// </summary>
    /// <returns><see langword="true"/> if the caller should idle-delay before the next iteration.</returns>
    private async Task<bool> RunSingleIterationAsync(bool isFirstInstance, CancellationToken stoppingToken, ref int consecutiveClaims)
    {
        // Additional (non-primary) tasks stay idle until the primary task has observed
        // a sustained backlog. This keeps polling load proportional to actual event volume
        // instead of always running TaskCount concurrent pollers.
        if (!isFirstInstance && !_manyEventsLately.Get())
        {
            return true;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();
        IRegisteredEventsProcessingService processingService =
            scope.ServiceProvider.GetRequiredService<IRegisteredEventsProcessingService>();

        bool processed = await processingService.TryProcessEvent(stoppingToken);

        if (!processed)
        {
            consecutiveClaims = 0;
            return true;
        }

        consecutiveClaims++;
        TrackRampUp(isFirstInstance, consecutiveClaims);
        return false;
    }

    /// <summary>
    /// Flips the shared ramp-up flag once the primary task reaches <see cref="EventsProcessingSettings.RampUpLimit"/>
    /// consecutive successful claims, allowing additional tasks to start polling too.
    /// </summary>
    private void TrackRampUp(bool isFirstInstance, int consecutiveClaims)
    {
        if (isFirstInstance && consecutiveClaims >= _settings.RampUpLimit)
        {
            _manyEventsLately.Set(true);
        }
    }

    /// <summary>
    /// A thread-safe flag indicating whether there have been many events processed lately,
    /// used to ramp up additional polling tasks once the primary task detects sustained backlog.
    /// </summary>
    private sealed class ManyEventsLately
    {
        private bool _manyEventsLately;
        private readonly object _lock = new();

        /// <summary>
        /// Gets the value of the flag in a thread-safe manner.
        /// </summary>
        public bool Get()
        {
            lock (_lock)
            {
                return _manyEventsLately;
            }
        }

        /// <summary>
        /// Sets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <param name="value">The value to set the flag to.</param>
        public void Set(bool value)
        {
            lock (_lock)
            {
                _manyEventsLately = value;
            }
        }
    }
}
