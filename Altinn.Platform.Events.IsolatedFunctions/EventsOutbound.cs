using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Services;

using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.IsolatedFunctions;

/// <summary>
/// Processes outbound events from a queue and sends them to a webhook endpoint.
/// </summary>
/// <remarks>This class is designed to handle events retrieved from an Azure Storage Queue. It processes each
/// event,  deserializes it into a cloud event envelope, and attempts to send it to a webhook endpoint using the  <see
/// cref="IWebhookService"/>. If the webhook delivery fails, the event is requeued with a backoff  strategy using the
/// <see cref="IRetryBackoffService"/>.</remarks>
/// <param name="webhookService">The service making the http calls</param>
/// <param name="retryBackoffService">The service that handles the exponential backoff retries</param>
public class EventsOutbound(IWebhookService webhookService, IRetryBackoffService retryBackoffService)
{
    private const string _queueName = "events-outbound";
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IRetryBackoffService _retryBackoffService = retryBackoffService;

    /// <summary>
    /// The function consumes messages from the "events-outbound" queue.
    /// </summary>
    /// <param name="item">A base64 decoded string representation of the payload</param>
    /// <returns></returns>
    [Function(nameof(EventsOutbound))]
    public async Task Run([QueueTrigger(_queueName, Connection = "AzureWebJobsStorage")] string item)
    {
        var retryableEventWrapper = item.DeserializeToRetryableEventWrapper();

        var cloudEventEnvelope = retryableEventWrapper != null ? CloudEventEnvelope.DeserializeToCloudEventEnvelope(retryableEventWrapper.Payload) : CloudEventEnvelope.DeserializeToCloudEventEnvelope(item);

        try
        {
            await _webhookService.Send(cloudEventEnvelope);
        }
        catch (Exception ex)
        {
            if (retryableEventWrapper != null)
            {
                await _retryBackoffService.RequeueWithBackoff(retryableEventWrapper, ex);
            }
            else
            {
                // If retryableEventWrapper is null, it means we are dealing with a legacy message format
                var initWrapper = new RetryableEventWrapper
                {
                    Payload = cloudEventEnvelope.Serialize(),
                    DequeueCount = 0,
                    CorrelationId = Guid.NewGuid().ToString(),
                    FirstProcessedAt = DateTime.UtcNow
                };

                await _retryBackoffService.RequeueWithBackoff(initWrapper, ex);
            }
        }
    }
}
