using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.Functions;

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
    /// <returns>An asynchronous task</returns>
    [Function(nameof(EventsOutbound))]
    public async Task Run([QueueTrigger(_queueName, Connection = "QueueStorage")] string item)
    {
        RetryableEventWrapper? wrapperCandidate = null;
        try
        {
            wrapperCandidate = item.DeserializeToRetryableEventWrapper();
            CloudEventEnvelope envelope;

            if (wrapperCandidate is not null && !string.IsNullOrWhiteSpace(wrapperCandidate.Payload))
            {
                envelope = CloudEventEnvelope.DeserializeToCloudEventEnvelope(wrapperCandidate.Payload);
            }
            else
            {
                // Treat input as a legacy envelope JSON, meaning without a wrapper object
                envelope = CloudEventEnvelope.DeserializeToCloudEventEnvelope(item);
                
                // Ensure wrapperCandidate is null in this branch to drive legacy requeue behavior
                wrapperCandidate = null;
            }

            await _webhookService.Send(envelope);
        }
        catch (Exception ex)
        {
            // If we had a valid wrapper with payload, requeue that; otherwise, wrap original input.
            var toRequeue = (wrapperCandidate is not null && !string.IsNullOrWhiteSpace(wrapperCandidate.Payload))
                ? wrapperCandidate
                : new RetryableEventWrapper
                {
                    Payload = item,
                };

            await _retryBackoffService.RequeueWithBackoff(toRequeue, ex);
        }
    }
}
