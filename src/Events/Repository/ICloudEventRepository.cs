using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// This interface describes the public contract of a repository implementation for <see cref="CloudEvent"/>
    /// </summary>
    public interface ICloudEventRepository
    {
        /// <summary>
        /// Creates an app cloud event in repository
        /// </summary>
        /// <param name="cloudEvent">The cloud event object</param>
        /// <param name="serializedCloudEvent">The json serialized cloud event</param>
        Task CreateAppEvent(CloudEvent cloudEvent, string serializedCloudEvent);

        /// <summary>
        /// Creates a cloud event in the repository.
        /// </summary>
        /// <param name="cloudEvent">The json serialized cloud event</param>
        Task CreateEvent(string cloudEvent);

        /// <summary>
        /// Calls a function to retrieve app cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, string subject, List<string> source, List<string> type, int size);

        /// <summary>
        /// Calls a function to retrieve cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetEvents(string after, List<string> source, List<string> type, string subject, int size);
    }
}
