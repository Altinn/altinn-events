using System.Threading.Tasks;
using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for sending HTTP requests to webhooks
    /// </summary>
    public interface IWebhookService
    {
        /// <summary>
        /// Sends a cloud event envelope to the specified webhook endpoint
        /// </summary>
        /// <param name="envelope">The cloud event envelope containing the event and endpoint information</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SendAsync(CloudEventEnvelope envelope);
    }

/// <summary>
/// Interface to send content to webhooks
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Send cloudevent to webhook
    /// </summary>
    /// <param name="envelope">CloudEventEnvelope, includes content and uri</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Send(CloudEventEnvelope envelope, CancellationToken cancellationToken);
}
}
