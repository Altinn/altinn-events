using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Altinn.Platform.Events.Telemetry;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    public class TelemetryClientTests
    {
        [Fact]
        public void Subscription_Metrics_AreRecordedSuccessfully()
        {
            var counterMeasurements = new List<(string Name, long Value)>();
            var histogramMeasurements = new List<(string Name, double Value)>();

            using var telemetry = new TelemetryClient();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == TelemetryClient.AppName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<long>(
                (inst, value, _, _) => counterMeasurements.Add((inst.Name, value)));

            listener.SetMeasurementEventCallback<double>(
                (inst, value, _, _) => histogramMeasurements.Add((inst.Name, value)));
            listener.Start();

            // Act
            telemetry.SubscriptionAuthFailed();
            telemetry.RecordSubscriptionEventProcessingDuration(123.4);

            listener.RecordObservableInstruments();
            listener.Dispose();

            // Assert
            Assert.Contains(histogramMeasurements, m => m.Name == "events.subscription.processing.duration" && m.Value == 123.4);
            Assert.Contains(counterMeasurements, m => m.Name == "events.subscription.authorization.failed" && m.Value == 1);
        }
    }
}
