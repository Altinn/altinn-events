using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Interface for the authorization service
    /// </summary>
    public interface IAuthorization
    {
        /// <summary>
        /// Authorizes and filters Altinn App events based on authorization
        /// </summary>
        /// <param name="cloudEvents">The list of events</param>
        /// <returns>A list of authorized events</returns>
        public Task<List<CloudEvent>> AuthorizeAltinnAppEvents(List<CloudEvent> cloudEvents);

        /// <summary>
        /// Authorizes and filters events based on authorization
        /// </summary>
        /// <param name="cloudEvents">The list of events</param>
        /// <returns>A list of authorized events</returns>
        public Task<List<CloudEvent>> AuthorizeEvents(List<CloudEvent> cloudEvents);

        /// <summary>
        /// Authorizes the currents user's right to publish the provided event
        /// </summary>
        /// <returns>A boolean indicating if the publisher is authorized or not</returns>
        public Task<bool> AuthorizePublishEvent(CloudEvent cloudEvent);

        /// <summary>
        /// Method to authorize access to an Altinn App event
        /// </summary>
        /// <param name="cloudEvent">The cloud event to authorize</param>
        /// <param name="consumer">The consumer of the event</param>
        /// <returns>A boolean indicating if the consumer is authorized or not</returns>
        public Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer);

        /// <summary>
        /// Method to authorize access to a cloud event
        /// </summary>
        /// <param name="cloudEvent">The cloud event to authorize</param>
        /// <param name="consumer">The consumer of the event</param>
        /// <returns>A boolean indicating if the consumer is authorized or not</returns>
        public Task<bool> AuthorizeConsumerForGenericEvent(CloudEvent cloudEvent, string consumer);

        /// <summary>
        /// Method to authorize access to create an Altinn App Events Subscription
        /// </summary>
        /// <param name="subscription">The subscription to be authorized containing source and consumer details</param>
        /// <returns>A boolean indicating if the consumer is authorized or not</returns>
        public Task<bool> AuthorizeConsumerForEventsSubscription(Subscription subscription);
    }
}
