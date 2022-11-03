using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// This interface describes the public contract of a repository implementation for <see cref="CloudEvent"/>
    /// </summary>
    public interface ICloudEventRepository
    {
        /// <summary>
        /// Creates an cloud event in repository
        /// </summary>
        /// <param name="cloudEvent">the cloud event object</param>
        Task CreateAppEvent(CloudEvent cloudEvent);

        /// <summary>
        /// Calls a function to retrieve app cloud events based on query params
        /// </summary>
        Task<List<CloudEvent>> GetAppEvent(string after, DateTime? from, DateTime? to, string subject, List<string> source, List<string> type, int size);
    }
}
