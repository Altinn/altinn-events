using System;
using System.Net.Http;
using System.Threading.Tasks;

using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Handlers
{
    /// <summary>
    /// Wolverine handler for processing subscription validation commands from Azure Service Bus.
    /// </summary>
    public class ValidateSubscriptionHandler
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ITraceLogService _traceLogService;
        private readonly IWebhookService _webhookService;
        private readonly ILogger<ValidateSubscriptionHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateSubscriptionHandler"/> class.
        /// </summary>
        public ValidateSubscriptionHandler(
            ISubscriptionService subscriptionService,
            ITraceLogService traceLogService,
            IWebhookService webhookService,
            ILogger<ValidateSubscriptionHandler> logger)
        {
            _subscriptionService = subscriptionService;
            _traceLogService = traceLogService;
            _webhookService = webhookService;
            _logger = logger;
        }

        /// <summary>
        /// Handles the ValidateSubscriptionCommand by sending a validation event to the subscription endpoint
        /// and marking the subscription as valid if successful.
        /// </summary>
        public async Task Handle(ValidateSubscriptionCommand command)
        {
            Subscription subscription = command.Subscription;

            _logger.LogInformation(
                "Handling ValidateSubscriptionCommand for subscription {SubscriptionId}, endpoint {Endpoint}",
                subscription.Id,
                subscription.EndPoint);

            CloudEvent validationEvent = CreateValidationEvent(subscription);
            CloudEventEnvelope envelope = CreateEnvelope(subscription, validationEvent);

            try
            {
                await _webhookService.SendAsync(envelope);

                (Subscription validatedSubscription, ServiceError error) =
                    await _subscriptionService.SetValidSubscription(subscription.Id);

                if (error != null)
                {
                    _logger.LogError(
                        "Failed to mark subscription {SubscriptionId} as valid, endpoint {Endpoint}. ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        subscription.Id,
                        subscription.EndPoint,
                        error.ErrorCode,
                        error.ErrorMessage);

                    await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                        validationEvent,
                        subscription,
                        TraceLogActivity.EndpointValidationFailed);

                    throw new InvalidOperationException($"Failed to validate subscription {subscription.Id}: {error.ErrorMessage}");
                }
                
                await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                    validationEvent,
                    subscription,
                    TraceLogActivity.EndpointValidationSuccess);

                _logger.LogInformation(
                    "Successfully validated subscription {SubscriptionId}, endpoint {Endpoint}",
                    subscription.Id,
                    subscription.EndPoint);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(
                    httpEx,
                    "Webhook validation failed for subscription {SubscriptionId}, endpoint {Endpoint}",
                    subscription.Id,
                    subscription.EndPoint);

                await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                    validationEvent,
                    subscription,
                    TraceLogActivity.EndpointValidationFailed);

                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while validating subscription {SubscriptionId}, endpoint {Endpoint}",
                    subscription.Id,
                    subscription.EndPoint);

                validationEvent = CreateValidationEvent(subscription);
                await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                    validationEvent,
                    subscription,
                    TraceLogActivity.EndpointValidationFailed);

                throw;
            }
        }

        private static CloudEvent CreateValidationEvent(Subscription subscription)
        {
            return new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri($"urn:altinn:events:subscriptions:{subscription.Id}"),
                Type = "platform.events.validatesubscription",
                Subject = subscription.Consumer,
                Time = DateTimeOffset.UtcNow,
                ["subscriptionId"] = subscription.Id,
                ["endpoint"] = subscription.EndPoint.ToString()
            };
        }

        private static CloudEventEnvelope CreateEnvelope(Subscription subscription, CloudEvent cloudEvent)
        {
            return new CloudEventEnvelope
            {
                CloudEvent = cloudEvent,
                Consumer = subscription.Consumer,
                Endpoint = subscription.EndPoint,
                SubscriptionId = subscription.Id,
                Pushed = DateTime.UtcNow
            };
        }
    }
}
