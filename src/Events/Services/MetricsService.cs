using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Service that retrieves and emits custom metrics.
    /// </summary>
    public class MetricsService : IMetricsService
    {
        /// <inheritdoc/>
        public Task TriggerGetMetrics(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
