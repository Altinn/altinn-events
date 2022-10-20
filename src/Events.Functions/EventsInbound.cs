using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Azure Function class.
    /// </summary>
    public class EventsInbound
    {
        private readonly IEventsClient _eventsClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsInbound"/> class.
        /// </summary>
        public EventsInbound(IEventsClient eventsClient)
        {
            _eventsClient = eventsClient;
        }

        /// <summary>
        /// Retrieves messages from events-inbound queue and push events controller
        /// </summary>
        [FunctionName("EventsInbound")]
#pragma warning disable IDE0060 // Remove unused parameter
        public async Task Run([QueueTrigger("events-inbound", Connection = "QueueStorage")] string item, ILogger log)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            CloudEvent cloudEvent = JsonSerializer.Deserialize<CloudEvent>(item);
            await _eventsClient.PostOutbound(cloudEvent);
        }
    }
}
