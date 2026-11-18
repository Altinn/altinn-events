using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Interface for the service that retrieves and emits custom metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Triggers retrieval and emission of custom metrics.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task TriggerGetMetrics(CancellationToken cancellationToken);
}
