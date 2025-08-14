using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Services;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.IsolatedFunctions;

public class EventsOutbound(ILogger<EventsOutbound> logger, IWebhookService webhookService, IRetryBackoffService retryBackoffService)
{
    private const string _queueName = "events-outbound";
    private readonly ILogger<EventsOutbound> _logger = logger;
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IRetryBackoffService _retryBackoffService = retryBackoffService;

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
                await _retryBackoffService.RequeueWithBackoff(retryableEventWrapper, ex, _queueName);
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

                await _retryBackoffService.RequeueWithBackoff(initWrapper, ex, _queueName);
            }
        }
    }
}
