using System;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;

public class CloudEventEnvelopeExtensionsTests
{
    [Fact]
    public void Serialize_NullCloudEvent_ThrowsInvalidOperationException()
    {
        // Arrange
        CloudEventEnvelope envelope = new()
        {
            CloudEvent = null,
            Consumer = "/org/ttd",
            Endpoint = new Uri("https://example.com"),
            SubscriptionId = 1
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => envelope.Serialize());
    }

    [Fact]
    public void Serialize_ValidEnvelope_ReturnsJsonWithCloudEvent()
    {
        // Arrange
        CloudEventEnvelope envelope = new()
        {
            CloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = "test-id",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Type = "test.type"
            },
            Consumer = "/org/ttd",
            Endpoint = new Uri("https://example.com"),
            SubscriptionId = 42
        };

        // Act
        string result = envelope.Serialize();

        // Assert
        Assert.Contains("\"CloudEvent\"", result);
        Assert.Contains("test-id", result);
        Assert.Contains("/org/ttd", result);
    }

    [Fact]
    public void Serialize_RestoresCloudEventAfterSerialization()
    {
        // Arrange
        var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = "test-id",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
            Type = "test.type"
        };

        CloudEventEnvelope envelope = new()
        {
            CloudEvent = cloudEvent,
            Consumer = "/org/ttd",
            Endpoint = new Uri("https://example.com"),
            SubscriptionId = 1
        };

        // Act
        envelope.Serialize();

        // Assert - CloudEvent should be restored
        Assert.NotNull(envelope.CloudEvent);
        Assert.Equal("test-id", envelope.CloudEvent.Id);
    }

    [Fact]
    public void DeserializeToEnvelope_NullInput_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((string)null).DeserializeToEnvelope());
    }

    [Fact]
    public void DeserializeToEnvelope_EmptyInput_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => string.Empty.DeserializeToEnvelope());
    }

    [Fact]
    public void DeserializeToEnvelope_InvalidJson_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAny<Exception>(() => "not valid json".DeserializeToEnvelope());
    }

    [Fact]
    public void DeserializeToEnvelope_MissingCloudEventProperty_ThrowsArgumentException()
    {
        // Arrange
        string json = "{\"Consumer\": \"/org/ttd\", \"SubscriptionId\": 1}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => json.DeserializeToEnvelope());
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTrip_PreservesData()
    {
        // Arrange
        CloudEventEnvelope original = new()
        {
            CloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = "roundtrip-id",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Type = "test.roundtrip"
            },
            Consumer = "/org/ttd",
            Endpoint = new Uri("https://example.com/webhook"),
            SubscriptionId = 99
        };

        // Act
        string serialized = original.Serialize();
        CloudEventEnvelope deserialized = serialized.DeserializeToEnvelope();

        // Assert
        Assert.Equal(original.Consumer, deserialized.Consumer);
        Assert.Equal(original.SubscriptionId, deserialized.SubscriptionId);
        Assert.Equal(original.CloudEvent.Id, deserialized.CloudEvent.Id);
        Assert.Equal(original.CloudEvent.Type, deserialized.CloudEvent.Type);
    }
}
