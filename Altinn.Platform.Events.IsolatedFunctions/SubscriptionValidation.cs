using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.IsolatedFunctions;

/// <summary>
/// Initializes a new instance of the <see cref="SubscriptionValidation"/> class.
/// </summary>
public class SubscriptionValidation(
    IOptions<PlatformSettings> eventsConfig,
    IWebhookService webhookService,
    IEventsClient eventsClient)
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly PlatformSettings _platformSettings = eventsConfig.Value;
    private readonly IEventsClient _eventsClient = eventsClient;

    /// <summary>
    /// Retrieves messages from subscription-validation queue and verify endpoint. If valid
    /// it will call subscription service
    /// </summary>
    [Function(nameof(SubscriptionValidation))]
    public async Task Run([QueueTrigger("subscription-validation", Connection = "QueueStorage")] string item)
    {
        Subscription subscription = Subscription.Deserialize(item);
        CloudEventEnvelope cloudEventEnvelope = CreateValidateEvent(subscription);
        await _webhookService.Send(cloudEventEnvelope);
        await _eventsClient.ValidateSubscription(cloudEventEnvelope.SubscriptionId);
    }

    /// <summary>
    /// Createas a cloud event envelope to wrap the subscription validation event
    /// </summary>
    internal CloudEventEnvelope CreateValidateEvent(Subscription subscription)
    {
        CloudEventEnvelope cloudEventEnvelope = new()
        {
            Consumer = subscription.Consumer,
            Endpoint = subscription.EndPoint,
            SubscriptionId = subscription.Id,
            CloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri(_platformSettings.ApiEventsEndpoint + "subscriptions/" + subscription.Id),
                Type = Functions.Constants.EventConstants.ValidationType,
            }
        };

        return cloudEventEnvelope;
    }
}
