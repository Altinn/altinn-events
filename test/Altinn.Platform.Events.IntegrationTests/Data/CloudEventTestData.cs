#nullable enable
using System;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.IntegrationTests.Data;

/// <summary>
/// Utility class for creating test CloudEvent instances in integration tests.
/// </summary>
public static class CloudEventTestData
{
    /// <summary>
    /// Creates a basic test CloudEvent with common Altinn app properties.
    /// </summary>
    /// <param name="id">Optional event ID. If not provided, a new GUID will be generated.</param>
    /// <returns>A CloudEvent configured for testing.</returns>
    public static CloudEvent CreateTestCloudEvent(string? id = null)
    {
        var cloudEvent = new CloudEvent
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Source = new Uri("https://ttd.apps.altinn.no/ttd/test-app/instances/12345/abcd-1234"),
            Type = "app.instance.created",
            Subject = "/party/12345",
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
        };

        cloudEvent["resource"] = "urn:altinn:resource:app_ttd_test-app";

        return cloudEvent;
    }
}
