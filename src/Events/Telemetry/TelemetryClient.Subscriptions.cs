using System.Diagnostics;

using static Altinn.Platform.Events.Telemetry.TelemetryClient.Subscriptions;

namespace Altinn.Platform.Events.Telemetry
{
    /// <summary>
    /// Extensions to the Telemetry class for metrics regardings subscriptions.
    /// </summary>
    partial class TelemetryClient
    {
        private void InitSubscriptionMetrics(InitContext context)
        {
            InitMetricCounter(context, MetricNameSubscriptionFailedAuth, init: static m => m.Add(0));
            InitHistogram(context, MetricNameEventProcessingDuration, "Time taken to process an event by subscribers");
        }

        /// <summary>
        /// Increments the counter for the amount of failed authorizations when checking subscriptions
        /// </summary>
        public void SubscriptionAuthFailed() => _counters[MetricNameSubscriptionFailedAuth].Add(1);

        /// <summary>
        /// Add a recorded duration for processing an event by subscribers.
        /// </summary>
        public void RecordSubscriptionEventProcessingDuration(double durationInMs) =>
            _histograms[MetricNameEventProcessingDuration].Record(durationInMs);

        /// <summary>
        /// This class holds a set of constants for the telemetry metrics of subscription processing.
        /// </summary>
        internal static class Subscriptions
        {
            /// <summary>
            /// The name of the metric for counting failed authorization checks against subscriptions.
            /// </summary>
            internal static readonly string MetricNameSubscriptionFailedAuth = MetricName("authorization.failed");

            /// <summary>
            /// The name of the metric for the duration of event processing by subscribers.
            /// </summary>
            internal static readonly string MetricNameEventProcessingDuration = MetricName("processing.duration");

            private static string MetricName(string name) => Metrics.CreateName($"subscription.{name}");
        }
    }
}
