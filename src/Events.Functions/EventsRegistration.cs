using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;

using CloudNative.CloudEvents;

using Microsoft.Azure.WebJobs;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Process incoming CloudEvents in "events-registration" queue.
    /// CloudEvents are first saved to the database
    /// before being added to the "events-inbound" queue.
    /// </summary>
    public class EventsRegistration
    {
        private readonly IEventsClient _eventsClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsInbound"/> class.
        /// </summary>
        public EventsRegistration(IEventsClient eventsClient)
        {
            _eventsClient = eventsClient;
        }

        /// <summary>
        /// Saves cloudEvents from events-registration queue to persistent storage
        /// and sends to events-inbound queue storage.
        /// </summary>
        [FunctionName("EventsRegistration")]
        public async Task Run([QueueTrigger("events-registration", Connection = "QueueStorage")] string item)
        {
            CloudEvent cloudEvent = item.DeserializeToCloudEvent();
            EnsureCorrectResourceFormat(cloudEvent);

            /*
            Attempt to save cloudEvent.
            If saving fails, the cloudEvent will automatically be
            returned to events-registration queue for retry handling.
            */

            await _eventsClient.SaveCloudEvent(cloudEvent);
            await _eventsClient.PostInbound(cloudEvent);
        }

        /// <summary>
        /// Changes . notation inresource for Altinn App events to use _.
        /// </summary>
        internal static void EnsureCorrectResourceFormat(CloudEvent cloudEvent)
        {
            if (cloudEvent["resource"].ToString().StartsWith("urn:altinn:resource:altinnapp."))
            {
                string org = null;
                string app = null;

                string[] pathParams = cloudEvent.Source.AbsolutePath.Split("/");

                if (pathParams.Length > 5)
                {
                    org = pathParams[1];
                    app = pathParams[2];
                }

                cloudEvent.SetAttributeFromString("resource", $"urn:altinn:resource:app_{org}_{app}");
            }
        }
    }
}
