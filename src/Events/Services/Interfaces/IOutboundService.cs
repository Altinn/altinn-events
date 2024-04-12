using System.Threading.Tasks;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Defines the methods required for an implementation of the Outbound service.
/// </summary>
public interface IOutboundService
{
    /// <summary>
    /// Deliver outbound cloudEvent to each matching subscriber.
    /// </summary>
    Task PostOutbound(CloudEvent cloudEvent);
}
