using System;
using System.Collections.Generic;
using System.Linq;

using Altinn.Platform.Events.Authorization;

using CloudNative.CloudEvents;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    public class GenericCloudEventXacmlMapperTests
    {
        [Fact]
        public void CreateMultipleResourceCategory_ConsecutiveCategoryIdsCreated()
        {
            // Arrange
            int expectedCategoryCount = 5;
            List<string> expectedCategoryIds = new List<string>() { "r1", "r2", "r3", "r4", "r5" };
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            cloudEvent["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";

            List<CloudEvent> events = new() { cloudEvent, cloudEvent, cloudEvent, cloudEvent, cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultipleResourceCategory(events);
            var actualCategoryIds = actual.Select(a => a.Id).ToList();

            // Assert
            Assert.Equal(expectedCategoryCount, actual.Count);
            Assert.Empty(expectedCategoryIds.Except(actualCategoryIds));
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithoutResourceInstance()
        {
            int expectedAttributeCount = 4;

            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            cloudEvent["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(cloudEvent);
            var actualEventIdAttribute = actual.Attribute.FirstOrDefault(a => a.AttributeId.Equals("urn:altinn:event-id"));

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:event-id"));
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:eventtype"));
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:eventsource"));
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:resource"));
            Assert.True(actualEventIdAttribute.IncludeInResult);
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithResourceInstance()
        {
            int expectedAttributeCount = 5;
            string expectedResourceId = Guid.NewGuid().ToString();

            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            cloudEvent["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";
            cloudEvent["resourceinstance"] = expectedResourceId;

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(cloudEvent);
            var actualResourceInstancedAttribute = actual.Attribute.FirstOrDefault(a => a.AttributeId.Equals("urn:altinn:resourceinstance"));

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Equal(expectedResourceId, actualResourceInstancedAttribute.Value);
        }
    }
}
