namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Settings for the DB-driven registered events processing background service.
/// </summary>
public class EventsProcessingSettings
{
    /// <summary>
    /// The maximum number of retry attempts for delivering an event to outbound before
    /// the event is flagged as <c>retryExhausted</c> and no longer retried.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;
}
