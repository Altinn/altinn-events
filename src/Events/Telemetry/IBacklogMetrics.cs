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

    /// <summary>
    /// Records the current number of events in status <c>Registered</c> and the age of the oldest of them.
    /// </summary>
    /// <param name="registeredCount">Number of events waiting to be processed.</param>
    /// <param name="oldestAgeSeconds">Age in seconds of the oldest registered event, or 0 if there are none.</param>
    void RecordRegistered(long registeredCount, double oldestAgeSeconds);
}
