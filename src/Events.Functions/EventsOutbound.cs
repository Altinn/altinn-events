using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Azure.WebJobs;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Azure Function class.
    /// </summary>
    public class EventsOutbound
    {
        private readonly IWebhookService _webhookService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsOutbound"/> class.
        /// </summary>
        public EventsOutbound(IWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        /// <summary>
        /// Retrieves messages from events-outbound queue and send to webhook
        /// </summary>
        [FunctionName("EventsOutbound")]
        public async Task Run([QueueTrigger("events-outbound", Connection = "QueueStorage")] string item)
        {
            var cloudEventEnvelope = CloudEventEnvelope.DeserializeToCloudEventEnvelope(item);

            await _webhookService.Send(cloudEventEnvelope);
        }
    }
}
