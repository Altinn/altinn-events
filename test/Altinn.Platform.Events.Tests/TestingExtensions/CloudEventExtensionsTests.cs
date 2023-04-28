using System;

using Altinn.Platform.Events.Extensions;

using CloudNative.CloudEvents;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;
    public class CloudEventExtensionsTests
    {
        [Fact]
        public void GetResource_ResourceIsDefined_StringIsReturned()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            ce["resource"] = "urn:altinn:resource:some-resource";

            // Act
            var actual = ce.GetResource();

            // Assert
            Assert.Equal("urn:altinn:resource:some-resource", actual);
        }

        [Fact]
        public void GetResource_ResourceIsNotDefined_NullIsReturned()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            // Act
            var actual = ce.GetResource();

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public void GetResource_ResourceInstanceIsDefined_StringIsReturned()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            ce["resourceinstance"] = "1337";

            // Act
            var actual = ce.GetResourceInstance();

            // Assert
            Assert.Equal("1337", actual);
        }

        [Fact]
        public void GetResource_ResourceInstanceIsNotDefined_NullIsReturned()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            // Act
            var actual = ce.GetResourceInstance();

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public void SetResource_ResourceNotDefined_AttributeSet()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            // Act
            ce.SetResourceIfNull("urn:altinn:resource:some-resource");

            // Assert
            Assert.NotNull(ce.GetResource());
        }

        [Fact]
        public void SetResource_ResourceAlreadyDefined_AttributeSet()
        {
            // Arrange
            CloudEvent ce = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            ce["resource"] = "urn:altinn:resource:some-resource";

            // Act
            ce.SetResourceIfNull("urn:altinn:resource:some-other-resource");

            // Assert
            Assert.Equal("urn:altinn:resource:some-resource", ce.GetResource());
        }
    }
}
