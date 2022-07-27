using System;

namespace Altinn.Platform.Events.Models
{
    /// <summary>
    /// Class that describes the events subscription request model
    /// </summary>
    public class SubscriptionRequestModel
    {
        /// <summary>
        /// Endpoint to receive matching events
        /// </summary>
        public Uri EndPoint { get; set; }

        /// <summary>
        /// Filter on source
        /// </summary>
        public Uri SourceFilter { get; set; }

        /// <summary>
        /// Filter on subject
        /// </summary>
        public string SubjectFilter { get; set; }

        /// <summary>
        /// Filter on alternative subject
        /// </summary>
        public string AlternativeSubjectFilter { get; set; }

        /// <summary>
        /// Filter for type. The different sources has different types. 
        /// </summary>
        public string TypeFilter { get; set; }
    }
}
