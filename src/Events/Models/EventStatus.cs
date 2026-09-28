namespace Altinn.Platform.Events.Models;

/// <summary>
/// Defines the possible statuses of an event in the system.
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// The event has been registered in the database, but not yet processed.
    /// </summary>
    Registered,

    /// <summary>
    /// The event has been claimed and processed.
    /// </summary>
    Processed,
    
    /// <summary>
    /// The event has reached the maximum number of retry attempts and will not be retried further.
    /// </summary>
    RetryExhausted
}
