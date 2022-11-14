using System;

using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Extension methods for Cloud Events from Altinn Apps
    /// </summary>
    public static class AppCloudEventExtensions
    {
        /// <summary>
        /// Create a Cloud Event based of the AppCloudEventRequestModel
        /// </summary>
        /// <returns>A cloud event</returns>
        public static CloudEvent CreateEvent(this AppCloudEventRequestModel appEvent)
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Time = DateTimeOffset.UtcNow,
                Source = appEvent.Source,
                Subject = appEvent.Subject,
                Type = appEvent.Type
            };

            if (!string.IsNullOrEmpty(appEvent.AlternativeSubject))
            {
                cloudEvent.SetAttributeFromString("alternativesubject", appEvent.AlternativeSubject);
            }

            return cloudEvent;
        }
    }
}
