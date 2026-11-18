using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Altinn.Platform.Events.Telemetry;

/// <summary>
/// Provides custom OpenTelemetry backlog metrics for events.
/// </summary>
/// <remarks>
/// Uses its own <see cref="Meter"/> so gauges can be added without touching <see cref="TelemetryClient"/>.
/// Keep tags low-cardinality: do not tag with subject or event id.
/// </remarks>
public sealed class BacklogMetrics : IBacklogMetrics, IDisposable
{
    /// <summary>
    /// The name of the OpenTelemetry meter. Must be registered via <c>AddMeter</c>
    /// in <c>Program.cs</c> so measurements are exported.
    /// </summary>
    public const string MeterName = "Altinn.Platform.Events.Backlog";

    /// <summary>
    /// The metric name for the number of events in status RetryExhausted.
    /// </summary>
    public static readonly string RetryExhaustedGaugeName = TelemetryClient.Metrics.CreateName("retry_exhausted");

    /// <summary>
    /// The metric name for the number of events in status Registered.
    /// </summary>
    public static readonly string RegisteredGaugeName = TelemetryClient.Metrics.CreateName("registered");

    /// <summary>
    /// The metric name for the age in seconds of the oldest event in status Registered.
    /// </summary>
    public static readonly string RegisteredOldestAgeGaugeName = TelemetryClient.Metrics.CreateName("registered.oldest_age");

    /// <summary>
    /// Key for the metric tag.
    /// </summary>
    public const string TagKey = "status";

    private const string _retryExhaustedTagValue = "retry_exhausted";
    private const string _registeredTagValue = "registered";

    private readonly Meter _meter;
    private readonly Gauge<long> _retryExhaustedGauge;
    private readonly Gauge<long> _registeredGauge;
    private readonly Gauge<double> _registeredOldestAgeGauge;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogMetrics"/> class.
    /// </summary>
    public BacklogMetrics()
    {
        _meter = new Meter(MeterName);
        _retryExhaustedGauge = _meter.CreateGauge<long>(RetryExhaustedGaugeName, description: "Current number of events in status RetryExhausted.");
        _registeredGauge = _meter.CreateGauge<long>(RegisteredGaugeName, description: "Current number of events in status Registered.");
        _registeredOldestAgeGauge = _meter.CreateGauge<double>(RegisteredOldestAgeGaugeName, unit: "s", description: "Age of the oldest event in status Registered.");
    }

    /// <inheritdoc/>
    public void RecordRetryExhausted(long retryExhaustedCount)
    {
        _retryExhaustedGauge.Record(retryExhaustedCount, new KeyValuePair<string, object>(TagKey, _retryExhaustedTagValue));
    }

    /// <inheritdoc/>
    public void RecordRegistered(long registeredCount, double oldestAgeSeconds)
    {
        var tag = new KeyValuePair<string, object>(TagKey, _registeredTagValue);
        _registeredGauge.Record(registeredCount, tag);
        _registeredOldestAgeGauge.Record(oldestAgeSeconds, tag);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
