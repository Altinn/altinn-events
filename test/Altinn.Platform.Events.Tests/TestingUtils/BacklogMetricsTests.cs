using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

using Altinn.Platform.Events.Telemetry;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    public class BacklogMetricsTests
    {
        [Fact]
        public void Record_Metrics_AreRecordedWithStatusTag()
        {
            var longMeasurements = new List<(string Name, long Value, string Tag)>();
            var doubleMeasurements = new List<(string Name, double Value, string Tag)>();

            using var backlogMetrics = new BacklogMetrics();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == BacklogMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<long>(
                (inst, value, tags, _) => longMeasurements.Add((inst.Name, value, tags.ToArray().Single(t => t.Key == BacklogMetrics.TagKey).Value as string)));
            listener.SetMeasurementEventCallback<double>(
                (inst, value, tags, _) => doubleMeasurements.Add((inst.Name, value, tags.ToArray().Single(t => t.Key == BacklogMetrics.TagKey).Value as string)));
            listener.Start();

            // Act
            backlogMetrics.RecordRetryExhausted(3);
            backlogMetrics.RecordRegistered(5, 12.5);

            // Assert
            Assert.Contains(longMeasurements, m => m.Name == "events.retry_exhausted" && m.Value == 3 && m.Tag == "retry_exhausted");
            Assert.Contains(longMeasurements, m => m.Name == "events.registered" && m.Value == 5 && m.Tag == "registered");
            Assert.Contains(doubleMeasurements, m => m.Name == "events.registered.oldest_age" && m.Value == 12.5 && m.Tag == "registered");
        }
    }
}
