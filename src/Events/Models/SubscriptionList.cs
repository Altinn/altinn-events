using System.Collections.Generic;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// An object containing a list of subscriptions and metadata
    /// </summary>
    public class SubscriptionList
    {
        /// <summary>
        /// The nuber of subscriptions in the list
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// The list of subscriptions
        /// </summary>
        public List<Subscription> Subscriptions { get; set; }
    }
}
