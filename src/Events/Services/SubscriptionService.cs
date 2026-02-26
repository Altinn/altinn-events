using System;
using System.Collections.Generic;
using System.Text.Json;
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
    private readonly IEventsQueueClient _queueClient;
    private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
    private readonly IAuthorization _authorization;
    private readonly PlatformSettings _platformSettings;
    private readonly WolverineSettings _wolverineSettings;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<SubscriptionService> _logger;
    private const string _organisationPrefix = "/organisation/";
    private const string _orgPrefix = "/org/";
    private const string _systemUserPrefix = "/systemuser/";
    private const string _userPrefix = "/user/";
    private const string _validationType = "platform.events.validatesubscription";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
    /// </summary>
    public SubscriptionService(
        ISubscriptionRepository repository,
        IAuthorization authorization,
        IMessageBus bus,
        IEventsQueueClient queueClient,
        IClaimsPrincipalProvider claimsPrincipalProvider,
        IOptions<PlatformSettings> platformSettings,
        IOptions<WolverineSettings> wolverineSettings,
        IWebhookService webhookService,
        ILogger<SubscriptionService> logger)
    {
        _repository = repository;
        _authorization = authorization;
        _bus = bus;
        _queueClient = queueClient;
        _claimsPrincipalProvider = claimsPrincipalProvider;
        _platformSettings = platformSettings.Value;
        _wolverineSettings = wolverineSettings.Value;
        _webhookService = webhookService;
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

        await PublishSubscriptionValidationEvent(subscription);

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

    /// <summary>
    /// Sends a validation event for the given subscription.
    /// </summary>
    public async Task SendAndValidate(Subscription subscription, CancellationToken cancellationToken)
    {
        CloudEventEnvelope cloudEventEnvelope = CreateValidateEvent(subscription);

        await _webhookService.Send(cloudEventEnvelope, cancellationToken);
        try 
        {
            (_, ServiceError error) = await SetValidSubscription(subscription.Id);
            if (error != null && error.ErrorCode == 404)
            {
                _logger.LogError("Attempting to validate non existing subscription {SubscriptionId}", subscription.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// Subscription // SetValidSubscription // Failed to validate subscription {SubscriptionId}", subscription.Id);
            throw new InvalidOperationException($"Failed to validate subscription with ID {subscription.Id}.", ex);
        }
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
                Type = _validationType
            }
        };

        return cloudEventEnvelope;
    }

    private async Task PublishSubscriptionValidationEvent(Subscription subscription)
    {
        if (_wolverineSettings.EnableServiceBus)
        {
            await _bus.PublishAsync(new ValidateSubscriptionCommand(subscription));
        }
        else
        {
            QueuePostReceipt receipt = await _queueClient.EnqueueSubscriptionValidation(JsonSerializer.Serialize(subscription));

            if (!receipt.Success)
            {
                throw receipt.Exception;
            }
        }
    }
}
