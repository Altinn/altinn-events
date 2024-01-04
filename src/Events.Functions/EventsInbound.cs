using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;

using CloudNative.CloudEvents;

using Microsoft.Azure.WebJobs;

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
        public async Task Run([QueueTrigger("events-inbound", Connection = "QueueStorage")] string item)
        {
            CloudEvent cloudEvent = item.DeserializeToCloudEvent();
            await _eventsClient.PostOutbound(cloudEvent);
        }
    }
}
