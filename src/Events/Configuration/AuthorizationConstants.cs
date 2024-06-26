﻿namespace Altinn.Platform.Events.Configuration
{
    /// <summary>
    /// Constants related to authorization of events
    /// </summary>
    public static class AuthorizationConstants
    {
        /// <summary>
        /// Id for policy platform access allowing access to APIs
        /// </summary>
        public const string POLICY_PLATFORM_ACCESS = "PlatformAccess";

        /// <summary>
        /// Id for policy scope for allowing publishing of events
        /// </summary>
        public const string POLICY_SCOPE_EVENTS_PUBLISH = "ScopeEventsPublish";

        /// <summary>
        /// Id for policy scope for allowing subscribing to events
        /// </summary>
        public const string POLICY_SCOPE_EVENTS_SUBSCRIBE = "ScopeEventsSubscribe";

        /// <summary>
        /// Id for policy requiring publish scope or platform access
        /// </summary>
        public const string POLICY_PUBLISH_SCOPE_OR_PLATFORM_ACCESS = "PublishScopeOrAccessToken";

        /// <summary>
        /// Scope for allowing subscribing to events
        /// </summary>
        public const string SCOPE_EVENTS_PUBLISH = "altinn:events.publish";

        /// <summary>
        /// Scope for allowing subscribing to events
        /// </summary>
        public const string SCOPE_EVENTS_SUBSCRIBE = "altinn:events.subscribe";

        /// <summary>
        /// Scope for allowing administrators to publish events without Altinn Authorization
        /// </summary>
        public const string SCOPE_EVENTS_ADMIN_PUBLISH = "altinn:events.publish.admin";

        /// <summary>
        /// The urn prefix for Altinn App resources
        /// </summary>
        public const string AppResourcePrefix = "urn:altinn:resource:app_";

        /// <summary>
        /// The urn prefix for Altinn App resources
        /// </summary>
        public const string AppResourceTemplate = "urn:altinn:resource:app_{0}_{1}";
    }
}
