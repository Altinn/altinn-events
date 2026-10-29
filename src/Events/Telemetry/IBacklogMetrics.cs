namespace Altinn.Platform.Events.Telemetry;

/// <summary>
/// Emits custom backlog metrics for events.
/// </summary>
public interface IBacklogMetrics
{
    /// <summary>
    /// Records the current number of events in status <c>RetryExhausted</c>.
    /// </summary>
    /// <param name="retryExhaustedCount">Number of events that have exhausted their retries.</param>
    void RecordRetryExhausted(long retryExhaustedCount);
}
