using Altinn.Platform.Events.Functions.Wolverine.Models;

namespace Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;

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
