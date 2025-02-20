using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for trace log service.
    /// </summary>
    public interface ITraceLogService
    {
        /// <summary>
        /// Creates a trace log entry based on registration of a new event
        /// </summary>
        /// <param name="cloudEvent">Cloud native CloudEvent <see cref="CloudEvent"/>></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation: Cloud event id.</returns>
        Task<string> CreateTraceLogRegisteredEntry(CloudEvent cloudEvent);
    }
}
