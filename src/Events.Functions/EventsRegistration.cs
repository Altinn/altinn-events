using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;
using CloudNative.CloudEvents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Functions;

/// <summary>
/// Processes incoming CloudEvents from the "events-registration" queue.
/// CloudEvents are first persisted and then routed via the Events API inbound endpoint
/// (which enqueues to the "events-inbound" queue).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EventsRegistration"/> class.
/// </remarks>
public class EventsRegistration(IEventsClient eventsClient, ILogger<EventsRegistration> logger)
{
    private readonly IEventsClient _eventsClient = eventsClient;
    private readonly ILogger<EventsRegistration> _logger = logger;

    /// <summary>
    /// Saves cloudEvents from events-registration queue to persistent storage
    /// and sends to events-inbound queue storage.
    /// </summary>
    [Function(nameof(EventsRegistration))]
    public async Task Run([QueueTrigger("events-registration", Connection = "QueueStorage")] string item)
    {
        _logger.LogWarning("Processing message from events-registration queue. {Item}", item);
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
        var resource = cloudEvent["resource"];

        if (resource is not null)
        {
            string? resourceValue = resource.ToString();
            if (resourceValue != null && resourceValue.StartsWith("urn:altinn:resource:altinnapp."))
            {
                string? org = null;
                string? app = null;

                string[] pathParams = cloudEvent.Source?.AbsolutePath.Split("/") ?? Array.Empty<string>();

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
