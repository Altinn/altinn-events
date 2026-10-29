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
    /// Key for the metric tag.
    /// </summary>
    public const string TagKey = "status";

    private const string _retryExhaustedTagValue = "retry_exhausted";

    private readonly Meter _meter;
    private readonly Gauge<long> _retryExhaustedGauge;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogMetrics"/> class.
    /// </summary>
    public BacklogMetrics()
    {
        _meter = new Meter(MeterName);
        _retryExhaustedGauge = _meter.CreateGauge<long>(RetryExhaustedGaugeName, description: "Current number of events in status RetryExhausted.");
    }

    /// <inheritdoc/>
    public void RecordRetryExhausted(long retryExhaustedCount)
    {
        _retryExhaustedGauge.Record(retryExhaustedCount, new KeyValuePair<string, object>(TagKey, _retryExhaustedTagValue));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
