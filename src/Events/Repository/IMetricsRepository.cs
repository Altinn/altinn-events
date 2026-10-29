using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Repository for retrieving data used for custom metrics.
/// </summary>
public interface IMetricsRepository
{
    /// <summary>
    /// Gets the current number of events in status <c>RetryExhausted</c>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The number of events that have exhausted their retries.</returns>
    Task<long> GetRetryExhaustedCount(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current number of events in status <c>Registered</c> and the age in seconds of the oldest of them.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The registered event count and the age in seconds of the oldest registered event, or 0 if there are none.</returns>
    Task<(long Count, double OldestAgeSeconds)> GetRegisteredEventsMetrics(CancellationToken cancellationToken);
}
