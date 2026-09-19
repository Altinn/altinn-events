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
public class RegisteredEventsBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<EventsProcessingSettings> settings,
    ILogger<RegisteredEventsBackgroundService> logger) : BackgroundService
{
    private readonly EventsProcessingSettings _settings = settings.Value;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IRegisteredEventsProcessingService processingService =
                    scope.ServiceProvider.GetRequiredService<IRegisteredEventsProcessingService>();

                bool processed = await processingService.TryProcessEvent(stoppingToken);

                if (!processed && !stoppingToken.IsCancellationRequested)
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
}
