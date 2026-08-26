using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Constants;
using Altinn.Platform.Events.Functions.Wolverine.Commands;
using Altinn.Platform.Events.Functions.Wolverine.Models;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Options;
using Wolverine.Attributes;

namespace Altinn.Platform.Events.Functions.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for processing subscription validation commands from Azure Service Bus.
/// Behaves like the Events API's in-process handler of the same name (send validation webhook,
/// then mark the subscription valid) but marks the subscription valid via an authenticated
/// callback to the Events API instead of a direct database write, since this app has no
/// database access.
/// </summary>
[WolverineHandler]
public static class ValidationEventHandler
{
    /// <summary>
    /// Handles the <see cref="ValidateSubscriptionCommand"/> by sending the validation webhook
    /// and then calling back to the Events API to mark the subscription valid.
    /// </summary>
    public static async Task Handle(
        ValidateSubscriptionCommand message,
        IOptions<PlatformSettings> platformSettings,
        IWebhookService webhookService,
        IEventsClient eventsClient,
        CancellationToken cancellationToken)
    {
        CloudEventEnvelope envelope = CreateValidateEvent(message.Subscription, platformSettings.Value);

        await webhookService.Send(envelope, cancellationToken);
        await eventsClient.ValidateSubscription(message.Subscription.Id);
    }

    /// <summary>
    /// Creates a cloud event envelope to wrap the subscription validation event.
    /// </summary>
    internal static CloudEventEnvelope CreateValidateEvent(Subscription subscription, PlatformSettings platformSettings)
    {
        CloudEventEnvelope cloudEventEnvelope = new()
        {
            Consumer = subscription.Consumer,
            Endpoint = subscription.EndPoint,
            SubscriptionId = subscription.Id,
            CloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri(platformSettings.ApiEventsEndpoint + "subscriptions/" + subscription.Id),
                Type = EventConstants.ValidationType
            }
        };

        return cloudEventEnvelope;
    }
}
