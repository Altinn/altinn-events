using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Telemetry;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service that retrieves and emits custom metrics.
    /// </summary>
    public class MetricsService(IMetricsRepository metricsRepository, IBacklogMetrics backlogMetrics) : IMetricsService
    {
        /// <inheritdoc/>
        public async Task TriggerGetMetrics(CancellationToken cancellationToken)
        {
            var (registeredCount, oldestAgeSeconds, retryExhaustedCount) = await metricsRepository.GetBacklogMetrics(cancellationToken);

            backlogMetrics.RecordRetryExhausted(retryExhaustedCount);
            backlogMetrics.RecordRegistered(registeredCount, oldestAgeSeconds);
        }
    }
}
