using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Repository for retrieving data used for custom metrics.
/// </summary>
public interface IMetricsRepository
{
    /// <summary>
    /// Gets the current backlog metrics: the number of events in status <c>Registered</c>,
    /// the age in seconds of the oldest of them, and the number of events in status <c>RetryExhausted</c>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The registered event count, the age in seconds of the oldest registered event (0 if there are none) and the retry exhausted count.</returns>
    Task<(long RegisteredCount, double RegisteredOldestAgeSeconds, long RetryExhaustedCount)> GetBacklogMetrics(CancellationToken cancellationToken);
}
