using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Altinn.Platform.Events.Services;

/// <inheritdoc/>
public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IMessageBus _bus;
    private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
    private readonly IAuthorization _authorization;
    private readonly IWebhookService _webhookService;
    private readonly ITraceLogService _traceLogService;
    private readonly PlatformSettings _platformSettings;
    private readonly ILogger<SubscriptionService> _logger;

    private const string _organisationPrefix = "/organisation/";
    private const string _orgPrefix = "/org/";
    private const string _systemUserPrefix = "/systemuser/";
    private const string _userPrefix = "/user/";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
    /// </summary>
    public SubscriptionService(
        ISubscriptionRepository repository,
        IAuthorization authorization,
        IMessageBus bus,
        IClaimsPrincipalProvider claimsPrincipalProvider,
        IWebhookService webhookService,
        ITraceLogService traceLogService,
        IOptions<PlatformSettings> platformSettings,
        ILogger<SubscriptionService> logger)
    {
        _repository = repository;
        _authorization = authorization;
        _bus = bus;
        _claimsPrincipalProvider = claimsPrincipalProvider;
        _webhookService = webhookService;
        _traceLogService = traceLogService;
        _platformSettings = platformSettings.Value;
        _logger = logger;
        }

    /// <summary>
    /// Completes the common tasks related to creating a subscription once the producer specific services are completed
    /// </summary>
    internal async Task<(Subscription Subscription, ServiceError Error)> CompleteSubscriptionCreation(Subscription eventsSubscription)
    {
        if (!await _authorization.AuthorizeConsumerForEventsSubscription(eventsSubscription))
        {
            var errorMessage = $"Not authorized to create a subscription for resource {eventsSubscription.ResourceFilter}.";
            return (null, new ServiceError(403, errorMessage));
        }

        Subscription subscription = await _repository.FindSubscription(eventsSubscription, CancellationToken.None);

        subscription ??= await _repository.CreateSubscription(eventsSubscription);

        await _bus.PublishAsync(new ValidateSubscriptionCommand(subscription));

        return (subscription, null);
    }

    /// <inheritdoc/>
    public async Task<ServiceError> DeleteSubscription(int id)
    {
        (Subscription subscription, ServiceError error) = await GetSubscription(id);

        if (error != null)
        {
            return error;
        }

        if (!AuthorizeAccessToSubscription(subscription))
        {
            error = new ServiceError(403);

            return error;
        }

        await _repository.DeleteSubscription(id);
        return null;
    }

    /// <inheritdoc/>
    public async Task<(Subscription Subscription, ServiceError Error)> GetSubscription(int id)
    {
        var subscription = await _repository.GetSubscription(id);

        if (subscription == null)
        {
            return (null, new ServiceError(404));
        }

        if (!AuthorizeAccessToSubscription(subscription))
        {
            return (null, new ServiceError(403));
        }

        return (subscription, null);
    }

    /// <inheritdoc/>
    public async Task<(List<Subscription> Subscription, ServiceError Error)> GetAllSubscriptions()
    {
        string consumer = GetEntityFromPrincipal();

        var subscriptions = await _repository.GetSubscriptionsByConsumer(consumer, true);

        return (subscriptions, null);
    }

    /// <inheritdoc/>
    public async Task<(Subscription Subscription, ServiceError Error)> SetValidSubscription(int id)
    {
        var subscription = await _repository.GetSubscription(id);

        if (subscription == null)
        {
            return (null, new ServiceError(404));
        }

        await _repository.SetValidSubscription(id);

        return (subscription, null);
    }

    /// <inheritdoc/>
    public async Task<ServiceError> SendAndValidate(Subscription subscription)
    {
        _logger.LogInformation(
            "Validating subscription {SubscriptionId}, endpoint {Endpoint}",
            subscription.Id,
            subscription.EndPoint);

        CloudEventEnvelope envelope = CreateValidateEvent(subscription);

        try
        {
            await _webhookService.SendAsync(envelope);

            (_, ServiceError error) = await SetValidSubscription(subscription.Id);

            if (error != null)
            {
                _logger.LogError(
                    "Failed to mark subscription {SubscriptionId} as valid, endpoint {Endpoint}. ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                    subscription.Id,
                    subscription.EndPoint,
                    error.ErrorCode,
                    error.ErrorMessage);

                await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                    envelope.CloudEvent,
                    subscription,
                    TraceLogActivity.EndpointValidationFailed);

                return error;
            }

            await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                envelope.CloudEvent,
                subscription,
                TraceLogActivity.EndpointValidationSuccess);

            _logger.LogInformation(
                "Successfully validated subscription {SubscriptionId}, endpoint {Endpoint}",
                subscription.Id,
                subscription.EndPoint);

            return null;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(
                httpEx,
                "Webhook validation failed for subscription {SubscriptionId}, endpoint {Endpoint}",
                subscription.Id,
                subscription.EndPoint);

            await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                envelope.CloudEvent,
                subscription,
                TraceLogActivity.EndpointValidationFailed);

            return new ServiceError(502, $"Webhook validation failed: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error occurred while validating subscription {SubscriptionId}, endpoint {Endpoint}",
                subscription.Id,
                subscription.EndPoint);

            await _traceLogService.CreateLogEntryWithSubscriptionDetails(
                envelope.CloudEvent,
                subscription,
                TraceLogActivity.EndpointValidationFailed);

            return new ServiceError(500, $"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a cloud event envelope to wrap the subscription validation event.
    /// </summary>
    private CloudEventEnvelope CreateValidateEvent(Subscription subscription)
    {
        return new CloudEventEnvelope
        {
            Consumer = subscription.Consumer,
            Endpoint = subscription.EndPoint,
            SubscriptionId = subscription.Id,
            CloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Source = new Uri(_platformSettings.ApiEventsEndpoint + "subscriptions/" + subscription.Id),
                Type = "platform.events.validatesubscription"
            }
        };
    }

    /// <summary>
    /// Retrieves the current entity based on the claims principal.
    /// </summary>
    internal string GetEntityFromPrincipal()
    {
        var user = _claimsPrincipalProvider.GetUser();

        string org = user.GetOrg();
        if (!string.IsNullOrEmpty(org))
        {
            return $"{_orgPrefix}{org}";
        }

        if (user.GetUserId() is string userId)
        {
            return $"{_userPrefix}{userId}";
        }

        if (user.GetSystemUserId() is Guid systemUserId && systemUserId != Guid.Empty)
        {
            return $"{_systemUserPrefix}{systemUserId}";
        }

        string organisation = user.GetOrganizationNumber();
        if (!string.IsNullOrEmpty(organisation))
        {
            return $"{_organisationPrefix}{organisation}";
        }

        return null;
    }

    private bool AuthorizeAccessToSubscription(Subscription eventsSubscription)
    {
        string currentIdenity = GetEntityFromPrincipal();
        return eventsSubscription.CreatedBy.Equals(currentIdenity);
    }
}
