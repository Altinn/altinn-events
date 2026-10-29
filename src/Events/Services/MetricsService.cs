using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service that retrieves and emits custom metrics.
    /// </summary>
    public class MetricsService(IMetricsRepository metricsRepository) : IMetricsService
    {
        /// <inheritdoc/>
        public async Task TriggerGetMetrics(CancellationToken cancellationToken)
        {
            var restryExahustedCount = await metricsRepository.GetRetryExhaustedCount(cancellationToken);

        }
    }
}
