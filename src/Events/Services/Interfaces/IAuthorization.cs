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
        public Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer);

        /// <summary>
        /// Method to authorize access to create an Altinn App Events Subscription
        /// </summary>
        public Task<bool> AuthorizeConsumerForEventsSubcription(Subscription subscription);
    }
}
