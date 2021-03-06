using System;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Functions
{
    /// <summary>
    /// Function to validate the webhook endpoint for an subscription
    /// </summary>
    public class SubscriptionValidation
    {
        private readonly IWebhookService _webhookService;
        private readonly PlatformSettings _platformSettings;
        private readonly IValidateSubscriptionService _validateSubscriptionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionValidation"/> class.
        /// </summary>
        public SubscriptionValidation(
            IWebhookService webhookService,
            IOptions<PlatformSettings> eventsConfig,
            IValidateSubscriptionService validateSubscriptionService)
        {
            _platformSettings = eventsConfig.Value;
            _webhookService = webhookService;
            _validateSubscriptionService = validateSubscriptionService;
        }

        /// <summary>
        /// Retrieves messages from subscription-validation queue and verify endpoint. If valid
        /// it will call subscription service
        /// </summary>
        [FunctionName("SubscriptionValidation")]
#pragma warning disable IDE0060 // Remove unused parameter
        public async Task Run([QueueTrigger("subscription-validation", Connection = "QueueStorage")] string item, ILogger log)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            Subscription subscription = JsonSerializer.Deserialize<Subscription>(item);
            CloudEventEnvelope cloudEventEnvelope = CreateValidateEvent(subscription);
            await _webhookService.Send(cloudEventEnvelope);
            await _validateSubscriptionService.ValidateSubscription(cloudEventEnvelope.SubscriptionId);
        }

        private CloudEventEnvelope CreateValidateEvent(Subscription subscription)
        {
            CloudEventEnvelope cloudEventEnvelope = new()
            {
                Consumer = subscription.Consumer,
                Endpoint = subscription.EndPoint,
                SubscriptionId = subscription.Id,
                CloudEvent = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = new Uri(_platformSettings.ApiEventsEndpoint + "subscriptions/" + subscription.Id),
                    Type = "platform.events.validatesubscription",
                    SpecVersion = "1.0"
                }
            };

            return cloudEventEnvelope;
        }
    }
}
