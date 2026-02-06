using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Wolverine;

namespace Altinn.Platform.Events.Services;

/// <inheritdoc/>
public class GenericSubscriptionService : SubscriptionService, IGenericSubscriptionService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
    /// </summary>
    public GenericSubscriptionService(
        ISubscriptionRepository repository,
        IAuthorization authorization,
        IMessageBus bus,
        IClaimsPrincipalProvider claimsPrincipalProvider)
        : base(repository, authorization, bus, claimsPrincipalProvider)
    {
    }

    /// <inheritdoc/>
    public async Task<(Subscription Subscription, ServiceError Error)> CreateSubscription(Subscription eventsSubscription)
    {
        string currentEntity = GetEntityFromPrincipal();
        eventsSubscription.CreatedBy = currentEntity;
        eventsSubscription.Consumer = currentEntity;

        if (!ValidateSubscription(eventsSubscription, out string message))
        {
            return (null, new ServiceError(400, message));
        }

        return await CompleteSubscriptionCreation(eventsSubscription);
    }

    private static bool ValidateSubscription(Subscription eventsSubscription, out string message)
    {
        if (string.IsNullOrEmpty(eventsSubscription.ResourceFilter))
        {
            message = "Resource filter is required.";
            return false;
        }

        if (eventsSubscription.SourceFilter != null)
        {
            message = "Source filter is not supported for subscriptions on this resource.";
            return false;
        }

        if (!string.IsNullOrEmpty(eventsSubscription.AlternativeSubjectFilter))
        {
            message = "AlternativeSubject filter is not supported for subscriptions on this resource.";
            return false;
        }

        message = null;
        return true;
    }
}
