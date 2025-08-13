using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.IsolatedFunctions
{
    public class EventsOutbound
    {
        private readonly IWebhookService _webhookService;

        public EventsOutbound(IWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [Function(nameof(EventsOutbound))]
        public async Task Run([QueueTrigger("events-outbound", Connection = "AzureWebJobsStorage")] string item)
        {
            var retryableEventWrapper = item.DeserializeToRetryableEventWrapper();

            var cloudEventEnvelope = retryableEventWrapper != null ? CloudEventEnvelope.DeserializeToCloudEventEnvelope(retryableEventWrapper.Payload) : CloudEventEnvelope.DeserializeToCloudEventEnvelope(item);

            await _webhookService.Send(cloudEventEnvelope);
        }
    }
}
