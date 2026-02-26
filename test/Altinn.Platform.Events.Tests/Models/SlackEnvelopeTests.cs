using System;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;
using Xunit;

namespace Altinn.Platform.Events.Tests.Models;

public class SlackEnvelopeTests
{
    [Fact]
    public void Serialize_WithValidCloudEvent_ReturnsCorrectJsonFormat()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "test-id-123",
            Source = new Uri("https://test.altinn.no/events"),
            Type = "test.event.type",
            Time = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Subject = "party/12345"
        };

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.StartsWith("{\"text\": \"", serialized);
        Assert.EndsWith("\"}", serialized);
        Assert.Contains("test-id-123", serialized);
        Assert.Contains("test.event.type", serialized);
    }

    [Fact]
    public void Serialize_WithNullCloudEvent_ReturnsEmptyJsonObject()
    {
        // Arrange
        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = null
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        Assert.Equal("{ }", serialized);
    }

    [Fact]
    public void Serialize_EscapesQuotesInCloudEvent()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "test-id",
            Source = new Uri("https://test.altinn.no/events"),
            Type = "test.type"
        };

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        // The CloudEvent JSON contains quotes that should be escaped
        Assert.Contains("\\\"", serialized);
        Assert.DoesNotContain("\"\"", serialized); // No double quotes without escape
    }

    [Fact]
    public void Serialize_WithComplexCloudEvent_HandlesAllProperties()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = new Uri("https://altinn.no/events/app/org/app-name"),
            Type = "app.instance.process.completed",
            Time = DateTime.UtcNow,
            Subject = "party/50001234",
            DataContentType = "application/json"
        };
        cloudEvent["resource"] = "urn:altinn:resource:app_org_app-name";
        cloudEvent["alternativesubject"] = "person/01234567890";

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.Contains(cloudEvent.Id, serialized);
        Assert.Contains(cloudEvent.Type, serialized);
        Assert.Contains("party/50001234", serialized);
        Assert.Contains("app.instance.process.completed", serialized);
    }

    [Fact]
    public void Serialize_ProducesValidSlackWebhookFormat()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "webhook-test",
            Source = new Uri("https://test.altinn.no"),
            Type = "test.webhook"
        };

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        // Verify it's valid JSON structure for Slack
        Assert.StartsWith("{", serialized);
        Assert.EndsWith("}", serialized);
        Assert.Contains("\"text\":", serialized);
    }

    [Fact]
    public void CloudEvent_Property_CanBeSetAndRetrieved()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "property-test",
            Source = new Uri("https://test.altinn.no"),
            Type = "test.property"
        };

        var slackEnvelope = new SlackEnvelope();

        // Act
        slackEnvelope.CloudEvent = cloudEvent;

        // Assert
        Assert.NotNull(slackEnvelope.CloudEvent);
        Assert.Equal("property-test", slackEnvelope.CloudEvent.Id);
        Assert.Equal("test.property", slackEnvelope.CloudEvent.Type);
    }

    [Fact]
    public void DefaultConstructor_CreatesInstanceWithNullCloudEvent()
    {
        // Act
        var slackEnvelope = new SlackEnvelope();

        // Assert
        Assert.NotNull(slackEnvelope);
        Assert.Null(slackEnvelope.CloudEvent);
    }

    [Fact]
    public void Serialize_WithSpecialCharactersInCloudEvent_EscapesCorrectly()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "special-chars-test",
            Source = new Uri("https://test.altinn.no"),
            Type = "test.special",
            Subject = "test/with\"quotes"
        };

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized = slackEnvelope.Serialize();

        // Assert
        Assert.NotNull(serialized);

        // Quotes should be escaped
        Assert.Contains("\\\"", serialized);
    }

    [Fact]
    public void Serialize_MultipleTimesWithSameEnvelope_ProducesSameResult()
    {
        // Arrange
        var cloudEvent = new CloudEvent
        {
            Id = "consistency-test",
            Source = new Uri("https://test.altinn.no"),
            Type = "test.consistency"
        };

        var slackEnvelope = new SlackEnvelope
        {
            CloudEvent = cloudEvent
        };

        // Act
        var serialized1 = slackEnvelope.Serialize();
        var serialized2 = slackEnvelope.Serialize();

        // Assert
        Assert.Equal(serialized1, serialized2);
    }
}
