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
using Altinn.Platorm.Events.Extensions;

namespace Altinn.Platform.Events.Services;

/// <inheritdoc/>
public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IEventsQueueClient _queue;
    private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
    private readonly IAuthorization _authorization;

    private const string OrganisationPrefix = "/organisation/";
    private const string OrgPrefix = "/org/";
    private const string SystemUserPrefix = "/systemser/";
    private const string UserPrefix = "/user/";

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
    /// Completes the common tasks related to creating a subcription once the producer specific services are completed
    /// </summary>
    internal async Task<(Subscription Subscription, ServiceError Error)> CompleteSubscriptionCreation(Subscription eventsSubscription)
    {
        if (!await _authorization.AuthorizeConsumerForEventsSubscription(eventsSubscription))
        {
            var errorMessage = $"Not authorized to create a subscription for resource {eventsSubscription.ResourceFilter}.";
            return (null, new ServiceError(401, errorMessage));
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
            error = new ServiceError(401);

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
            return (null, new ServiceError(401));
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
            return OrgPrefix + org;
        }

        int? userId = user.GetUserId();
        if (userId.HasValue)
        {
            return UserPrefix + userId.Value;
        }

        Guid? systemUserId = user.GetSystemUserId();
        if (systemUserId.HasValue && systemUserId.Value != Guid.Empty)
        {
            return SystemUserPrefix + systemUserId;
        }

        string organisation = user.GetOrgNumber();
        if (!string.IsNullOrEmpty(organisation))
        {
            return OrganisationPrefix + organisation;
        }

        return null;
    }

    private bool AuthorizeAccessToSubscription(Subscription eventsSubscription)
    {
        string currentIdenity = GetEntityFromPrincipal();
        return eventsSubscription.CreatedBy.Equals(currentIdenity);
    }
}
