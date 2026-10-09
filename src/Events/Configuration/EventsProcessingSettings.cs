namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Settings for the DB-driven registered events processing background service.
/// </summary>
public class EventsProcessingSettings
{
    /// <summary>
    /// The number of concurrent polling tasks to run.
    /// </summary>
    public int TaskCount { get; set; } = 30;

    /// <summary>
    /// The delay, in seconds, between polling iterations for the primary (first-started) task
    /// when no event was available to claim.
    /// </summary>
    public int PrimaryTaskIdleDelaySeconds { get; set; } = 2;

    /// <summary>
    /// The delay, in seconds, between polling iterations for additional (non-primary) tasks
    /// when no event was available to claim.
    /// </summary>
    public int AdditionalTasksIdleDelaySeconds { get; set; } = 5;

    /// <summary>
    /// The number of consecutive successful claims by the primary task required before
    /// additional (non-primary) tasks start polling too. Until this threshold is reached,
    /// only the primary task attempts to claim events.
    /// </summary>
    public int RampUpLimit { get; set; } = 5;

    /// <summary>
    /// The maximum number of retry attempts for delivering an event to outbound before
    /// the event is flagged as <c>retryExhausted</c> and no longer retried.
    /// </summary>
    /// <remarks>
    /// Enforced via <see cref="Repository.ICloudEventRepository.MarkEventRetryAsync"/>, which is
    /// called by <see cref="Services.RegisteredEventsProcessingService"/> whenever processing a
    /// claimed event fails; the retry count is incremented and the event is marked
    /// <c>retryExhausted</c> once it reaches this limit.
    /// </remarks>
    public int MaxRetryCount { get; set; } = 5;
}
