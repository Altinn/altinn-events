namespace Altinn.Platform.Events.Configuration
{
    /// <summary>
    /// Constants related to authorization of events
    /// </summary>
    public static class AuthorizationConstants
    {
        /// <summary>
        /// Id for policy scope for allowing publishing of events
        /// </summary>
        public const string POLICY_SCOPE_EVENTS_PUBLISH = "ScopeEventsPublish";

        /// <summary>
        /// Scope for allowing subscribing to events
        /// </summary>
        public const string SCOPE_EVENTS_PUBLISH = "altinn:events.publish";

        /// <summary>
        /// Scope for allowing subscribing to events
        /// </summary>
        public const string SCOPE_EVENTS_SUBSCRIBE = "altinn:events.subscribe";
    }
}
