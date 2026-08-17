using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Wolverine.Publishers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services;

/// <inheritdoc/>
public class GenericSubscriptionService : SubscriptionService, IGenericSubscriptionService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenericSubscriptionService"/> class.
    /// </summary>
    public GenericSubscriptionService(
        ISubscriptionRepository repository,
        IAuthorization authorization,
        IClaimsPrincipalProvider claimsPrincipalProvider,
        IOptions<PlatformSettings> platformSettings,
        ISubscriptionValidationPublisher publisher,
        IWebhookService webhookService,
        ILogger<GenericSubscriptionService> logger)
        : base(repository, authorization, claimsPrincipalProvider, platformSettings, publisher, webhookService, logger)
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

        if (eventsSubscription.IncludeSubunits && string.IsNullOrEmpty(eventsSubscription.SubjectFilter))
        {
            message = "IncludeSubunits requires a subject filter.";
            return false;
        }

        message = null;
        return true;
    }
}
