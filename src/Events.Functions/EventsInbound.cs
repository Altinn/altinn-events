using System.Threading.Tasks;
using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Extensions;
using CloudNative.CloudEvents;
using Microsoft.Azure.Functions.Worker;

namespace Altinn.Platform.Events.Functions;

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
    public async Task Run([ServiceBusTrigger("%InboundQueueName%", Connection = "ServiceBusConnection")] string item)
    {
        CloudEvent cloudEvent = item.DeserializeToCloudEvent();
        await _eventsClient.PostOutbound(cloudEvent);
    }
}
