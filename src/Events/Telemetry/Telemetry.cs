using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Altinn.Platform.Events.Telemetry
{
    /// <summary>
    /// Used for creating traces and metrics for the application.
    /// </summary>
    /// <remarks>
    /// This class is heavily inspired by the OpenTelemetry implementation in App.Lib.Core
    /// This class holds all labels, metrics and trace datastructures for OTel based instrumentation.
    /// There are a couple of reasons to do this:
    /// * Decouple metric lifetime from the objects that use them (since they are often scoped/transient)
    /// * Create a logical boundary to emphasize that telemetry and label names are considered public contract, subject to semver same as the rest of the code
    /// * Reason being that users may refer to these names in alerts, dashboards, saved queries etc
    /// * Minimize cluttering of "business logic" with instrumentation code
    ///
    /// Watch out for high cardinality when choosing tags. Most timeseries and tracing databases
    /// do not handle high cardinality well.
    /// </remarks>
    public sealed partial class Telemetry : IDisposable
    {
        /// <summary>
        /// A unique name of the application of use in the telemetry context.
        /// </summary>
        public const string AppName = "platform-events";

        private const string _metricPrefix = "events";

        private bool _isDisposed;
        private bool _isInitialized;

        private readonly object _lock = new();

        /// <summary>
        /// Gets the ActivitySource for the app.
        /// Using this, you can create traces that are transported to the OpenTelemetry collector.
        /// </summary>
        public ActivitySource ActivitySource { get; }

        /// <summary>
        /// Gets the Meter for the app.
        /// Using this, you can create metrics that are transported to the OpenTelemetry collector.
        /// </summary>
        public Meter Meter { get; }

        private FrozenDictionary<string, Counter<long>> _counters;

        /// <summary>
        /// Initializes a new instance of the <see cref="Telemetry"/> class.
        /// </summary>
        public Telemetry()
        {
            ActivitySource = new ActivitySource(AppName);
            Meter = new Meter(AppName);

            _counters = FrozenDictionary<string, Counter<long>>.Empty;

            Init();
        }

        /// <summary>
        /// Initializes the telemetry object.
        /// </summary>
        internal void Init()
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;

                var counters = new Dictionary<string, Counter<long>>();
                var context = new InitContext(counters);

                // TODO: InitContactRegisterUpdateJob(context);

                // NOTE: This Telemetry class is registered as a singleton
                // Metrics could be kept in fields of the respective objects that use them for instrumentation
                // but most of these objects have scoped or transient lifetime, which would be inefficient.
                // So instead they are kept in frozen dicts here and looked up as they are incremented.
                // Another option would be to keep them as plain fields here
                _counters = counters.ToFrozenDictionary();
            }
        }

        private readonly record struct InitContext(
            Dictionary<string, Counter<long>> Counters);

        /// <summary>
        /// Utility methods for creating metrics.
        /// </summary>
        public static class Metrics
        {
            /// <summary>
            /// Creates a name for a metric.
            /// </summary>
            /// <param name="name">Name of the metric, separate words with dot.</param>
            /// <returns>Full metric name</returns>
            public static string CreateName(string name) => $"{_metricPrefix}.{name}";
        }

        private void InitMetricCounter(InitContext context, string name, Action<Counter<long>> init)
        {
            // NOTE: There is an initialization function here mostly to zero-init counters.
            // This is useful in a prometheus-setting due to the 'increase' operator being a bit strange:
            // * none -> 1 does not count as an increase
            // * 0 -> 1 does count as an increase
            var counter = Meter.CreateCounter<long>(name, unit: null, description: null);
            context.Counters.Add(name, counter);
            init(counter);
        }

        /// <summary>
        /// Disposes the Telemetry object.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                ActivitySource?.Dispose();
                Meter?.Dispose();
            }
        }
    }
}
