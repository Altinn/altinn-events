using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Mappers;

/// <summary>
/// A class that holds the subscription mapper configurations
/// </summary>
public static class SubscriptionMapper
{
    /// <summary>
    /// Maps a <see cref="SubscriptionRequestModel"/> to a <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscriptionRequest">The subscription request to map</param>
    /// <returns>A mapped subscription</returns>
    public static Subscription MapToSubscription(this SubscriptionRequestModel subscriptionRequest)
    {
        return new Subscription
        {
            EndPoint = subscriptionRequest.EndPoint,
            SourceFilter = subscriptionRequest.SourceFilter,
            SubjectFilter = subscriptionRequest.SubjectFilter,
            ResourceFilter = subscriptionRequest.ResourceFilter,
            AlternativeSubjectFilter = subscriptionRequest.AlternativeSubjectFilter,
            TypeFilter = subscriptionRequest.TypeFilter,
            IncludeSubunits = subscriptionRequest.IncludeSubunits,
        };
    }
}
