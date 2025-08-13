using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using CloudNative.CloudEvents;
using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.IsolatedFunctions;

/// <summary>
/// Process incoming CloudEvents in "events-registration" queue.
/// CloudEvents are first saved to the database
/// before being added to the "events-inbound" queue.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EventsInbound"/> class.
/// </remarks>
public class EventsRegistration(IEventsClient eventsClient)
{
    private readonly IEventsClient _eventsClient = eventsClient;

    /// <summary>
    /// Saves cloudEvents from events-registration queue to persistent storage
    /// and sends to events-inbound queue storage.
    /// </summary>
    [Function(nameof(EventsRegistration))]
    public async Task Run([QueueTrigger("events-registration", Connection = "AzureWebJobsStorage")] string item)
    {
        RetryableEventWrapper? eventWrapper = item.DeserializeToRetryableEventWrapper();
        CloudEvent cloudEvent = eventWrapper != null ? eventWrapper.ExtractCloudEvent() : item.DeserializeToCloudEvent();

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
