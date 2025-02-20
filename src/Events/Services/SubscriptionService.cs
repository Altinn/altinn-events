using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

namespace Altinn.Platform.Events.Services;

/// <inheritdoc/>
public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IEventsQueueClient _queue;
    private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
    private readonly IAuthorization _authorization;

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
        IEventsQueueClient queue,
        IClaimsPrincipalProvider claimsPrincipalProvider)
    {
        _repository = repository;
        _authorization = authorization;
        _queue = queue;
        _claimsPrincipalProvider = claimsPrincipalProvider;
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

        subscription ??= await _repository.CreateSubscription(eventsSubscription, eventsSubscription.SourceFilter?.GetMD5Hash());

        await _queue.EnqueueSubscriptionValidation(JsonSerializer.Serialize(subscription));

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
