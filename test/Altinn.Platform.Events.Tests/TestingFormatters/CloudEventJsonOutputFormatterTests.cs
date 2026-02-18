using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Formatters;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingFormatters;

public class CloudEventJsonOutputFormatterTests
{
    private static readonly CloudEvent TestCloudEvent = new(CloudEventsSpecVersion.V1_0)
    {
        Id = "test-id",
        Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
        Type = "test.type"
    };

    [Fact]
    public async Task WriteResponseBodyAsync_SingleCloudEvent_SerializesCorrectly()
    {
        // Arrange
        var formatter = new CloudEventJsonOutputFormatter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var context = new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding),
            typeof(CloudEvent),
            TestCloudEvent);

        // Act
        await formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        string result = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("test-id", result);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_CloudEventCollection_SerializesWithBrackets()
    {
        // Arrange
        var formatter = new CloudEventJsonOutputFormatter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var events = new List<CloudEvent>
        {
            new(CloudEventsSpecVersion.V1_0)
            {
                Id = "event-1",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Type = "test.type"
            },
            new(CloudEventsSpecVersion.V1_0)
            {
                Id = "event-2",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Type = "test.type"
            }
        };

        var context = new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding),
            typeof(List<CloudEvent>),
            events);

        // Act
        await formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        string result = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
        Assert.Contains("event-1", result);
        Assert.Contains("event-2", result);
        Assert.Contains(", ", result);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_SingleItemCollection_NoTrailingComma()
    {
        // Arrange
        var formatter = new CloudEventJsonOutputFormatter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var events = new List<CloudEvent>
        {
            new(CloudEventsSpecVersion.V1_0)
            {
                Id = "only-event",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Type = "test.type"
            }
        };

        var context = new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding),
            typeof(List<CloudEvent>),
            events);

        // Act
        await formatter.WriteResponseBodyAsync(context, Encoding.UTF8);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        string result = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(", ", result);
    }

    [Fact]
    public void CanWriteType_NonCloudEventType_ReturnsFalse()
    {
        // Arrange
        var formatter = new TestableCloudEventJsonOutputFormatter();

        // Act & Assert
        Assert.False(formatter.TestCanWriteType(typeof(string)));
    }

    [Fact]
    public void CanWriteType_CloudEventType_ReturnsTrue()
    {
        // Arrange
        var formatter = new TestableCloudEventJsonOutputFormatter();

        // Act & Assert
        Assert.True(formatter.TestCanWriteType(typeof(CloudEvent)));
    }

    [Fact]
    public void CanWriteType_CloudEventCollectionType_ReturnsTrue()
    {
        // Arrange
        var formatter = new TestableCloudEventJsonOutputFormatter();

        // Act & Assert
        Assert.True(formatter.TestCanWriteType(typeof(List<CloudEvent>)));
    }

    /// <summary>
    /// Exposes the protected CanWriteType method for testing.
    /// </summary>
    private class TestableCloudEventJsonOutputFormatter : CloudEventJsonOutputFormatter
    {
        public bool TestCanWriteType(Type type) => CanWriteType(type);
    }
}
