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
        private static string _appResourcePrefix = "urn:altinn:resource:altinnapp.";  

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

            var sourceProperties = GetPropertiesFromAppSource(appEvent.Source);

            cloudEvent.SetAttributeFromString("resource", $"{_appResourcePrefix}{sourceProperties.Org}.{sourceProperties.App}");
            cloudEvent.SetAttributeFromString("resourceinstance", $"{sourceProperties.InstanceOwnerPartyId}/{sourceProperties.InstanceGuid}");

            if (!string.IsNullOrEmpty(appEvent.AlternativeSubject))
            {
                cloudEvent.SetAttributeFromString("alternativesubject", appEvent.AlternativeSubject);
            }

            return cloudEvent;
        }

        /// <summary>
        /// Decomposes the source of a cloud event into known parts.
        /// </summary>
        /// <param name="source">The uri source of the cloud event</param>
        /// <returns>A tuple containing org, app, intanceOwnerPartyId and instanceGuid</returns>
        /// <remarks>Domain should always be validated as a match to current environment before using this method.</remarks>
        public static (string Org, string App, string InstanceOwnerPartyId, string InstanceGuid) GetPropertiesFromAppSource(Uri source)
        {
            string org = null;
            string app = null;
            string instanceOwnerPartyId = null;
            string instanceGuid = null;

            string[] pathParams = source.AbsolutePath.Split("/");

            if (pathParams.Length > 5)
            {
                org = pathParams[1];
                app = pathParams[2];
                instanceOwnerPartyId = pathParams[4];
                instanceGuid = pathParams[5];
            }

            return (org, app, instanceOwnerPartyId, instanceGuid);

        }
    }
}
