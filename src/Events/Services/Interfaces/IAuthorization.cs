using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for the authorization service
    /// </summary>
    public interface IAuthorization
    {
        /// <summary>
        /// Authorizes and filters events based on authorization
        /// </summary>
        /// <param name="consumer">The event consumer</param>
        /// <param name="cloudEvents">The list of events</param>
        /// <returns>A list of authorized events</returns>
        public Task<List<CloudEvent>> AuthorizeEvents(ClaimsPrincipal consumer, List<CloudEvent> cloudEvents);

        /// <summary>
        /// Method to authorize access to an Altinn App event
        /// </summary>
        /// <param name="cloudEvent">The cloud event to authorize</param>
        /// <param name="consumer">The consumer of the event</param>
        /// <returns>A boolean indicating if the consumer is authorized or not</returns>
        public Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer);

        /// <summary>
        /// Method to authorize access to create an Altinn App Events Subscription
        /// </summary>
        /// <param name="subscription">The subscription to be authorized containing source and consumer details</param>
        /// <returns>A boolean indicating if the consumer is authorized or not</returns>
        public Task<bool> AuthorizeConsumerForEventsSubcription(Subscription subscription);
    }
}
