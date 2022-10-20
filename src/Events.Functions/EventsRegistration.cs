using System;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Process incoming CloudEvents in "events-registration" queue.
    /// CloudEvents are first saved to the database
    /// before being added to the "events-inbound" queue.
    /// </summary>
    public class EventsRegistration
    {
        private readonly IEventsStorageClient _eventsStorageClient;
        private readonly IInboundClient _inboundQueueClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsInbound"/> class.
        /// </summary>
        public EventsRegistration(
            IEventsStorageClient eventsStorageClient,
            IInboundClient inboundQueueClient)
        {
            _eventsStorageClient = eventsStorageClient;
            _inboundQueueClient = inboundQueueClient;
        }

        /// <summary>
        /// Saves cloudEvents from events-registration queue to persistent storage
        /// and sends to events-inbound queue storage.
        /// </summary>
        [FunctionName("EventsRegistration")]
#pragma warning disable IDE0060 // Remove unused parameter
        public async Task Run([QueueTrigger("events-registration", Connection = "QueueStorage")] string item, ILogger log)
#pragma warning restore IDE0060 // Remove unused parameter
        {
                CloudEvent cloudEvent = JsonSerializer.Deserialize<CloudEvent>(item);

                /*
                Attempt to save cloudEvent. 
                If saving fails, the cloudEvent will automatically be 
                returned to events-registration queue for retry handling.
                */

                await _eventsStorageClient.SaveCloudEvent(cloudEvent);
                await _inboundQueueClient.PostInbound(cloudEvent);
            }
    }
}
