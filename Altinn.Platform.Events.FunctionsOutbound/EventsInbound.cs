using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using CloudNative.CloudEvents;
using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.IsolatedFunctions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventsInbound"/> class.
    /// </summary>
    public class EventsInbound(IEventsClient eventsClient)
    {
        private readonly IEventsClient _eventsClient = eventsClient;

        /// <summary>
        /// Retrieves messages from events-inbound queue and push events controller
        /// </summary>
        [Function(nameof(EventsInbound))]
        public async Task Run([QueueTrigger("events-inbound", Connection = "QueueStorage")] string item)
        {
            RetryableEventWrapper? eventWrapper = item.DeserializeToRetryableEventWrapper();

            CloudEvent cloudEvent = eventWrapper != null ? eventWrapper.ExtractCloudEvent() : item.DeserializeToCloudEvent();
            await _eventsClient.PostOutbound(cloudEvent);
        }
    }
}
