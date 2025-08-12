using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.FunctionsOutbound
{
    public class EventsOutbound
    {
        private readonly IWebhookService _webhookService;

        public EventsOutbound(IWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [Function(nameof(EventsOutbound))]
        public async Task Run([QueueTrigger("events-outbound", Connection = "QueueStorage")] string item)
        {
            var cloudEventEnvelope = CloudEventEnvelope.DeserializeToCloudEventEnvelope(item);

            await _webhookService.Send(cloudEventEnvelope);
        }
    }
}
