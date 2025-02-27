using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// Interface for the trace log repository
    /// </summary>
    public interface ITraceLogRepository
    {
        /// <summary>
        /// Creates a trace log entry
        /// </summary>
        /// <returns></returns>
        Task CreateTraceLogEntry(TraceLog traceLog);
    }
}
