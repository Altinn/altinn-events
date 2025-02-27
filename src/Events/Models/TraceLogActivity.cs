namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Enum for trace log activity
    /// </summary>
    public enum TraceLogActivity
    {
        /// <summary>
        /// Event is registered 
        /// </summary>
        Registered,

        /// <summary>
        /// Response from post to consumer endpoint
        /// </summary>
        WebhookPostResponse,

        /// <summary>
        /// Trace log when is sent to outbound queue
        /// </summary>
        OutboundQueue,

        /// <summary>
        /// Subscriber was unauthorized for the event
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Number of retries for the event has been reached
        /// </summary>
        DequeueLimitReached,

        /// <summary>
        /// Subscription is invalid
        /// </summary>
        InvalidSubscription
    }
}
