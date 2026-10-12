using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Processes a single registered event by claiming it from the database and forwarding
/// it to outbound delivery.
/// </summary>
public interface IRegisteredEventsProcessingService
{
    /// <summary>
    /// Attempts to claim and process a single registered event.
    /// </summary>
    /// <returns>true if an event was claimed and processed; false if no registered event was available.</returns>
    Task<bool> TryProcessEvent(CancellationToken cancellationToken);
}
