using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Tests.Utils;

using CloudNative.CloudEvents;

using Xunit;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    public class GenericCloudEventXacmlMapperTests
    {
        private readonly CloudEvent _cloudEvent;
        private readonly CloudEvent _cloudEventWithResourceInstance;

        public GenericCloudEventXacmlMapperTests()
        {
            _cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            _cloudEvent["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";

            _cloudEventWithResourceInstance = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            _cloudEventWithResourceInstance["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";
            _cloudEventWithResourceInstance["resourceinstance"] = "resourceInstanceId";
        }

        [Fact]
        public void CreateMultiDecisionRequest_AssertActionCategory()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);
            List<CloudEvent> events = new() { _cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, events).Request.Action;
            var actualAction = actual.First();
            var actualActionValue = actualAction.Attribute.Where(a => a.AttributeId.Equals(MatchAttributeIdentifiers.ActionId)).Select(a => a.Value).FirstOrDefault();

            Assert.Single(actual);
            Assert.Equal("a1", actualAction.Id);
            Assert.Equal("subscribe", actualActionValue);
        }

        [Fact]
        public void CreateMultiDecisionRequest_AssertSubjectCategory()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);
            List<CloudEvent> events = new() { _cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, events).Request.AccessSubject;
            var actualSubject = actual.First();

            // only asserting id. Remaning attributes set by PEP.
            // Should we verify to catch breaking or unexpected changes in dependency?     
            Assert.Single(actual);
            Assert.Equal("s1", actualSubject.Id);
        }

        [Fact]
        public void CreateMultiDecisionRequest_AssertMultiRequest()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);
            List<CloudEvent> events = new() { _cloudEvent, _cloudEvent, _cloudEvent, _cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, events).Request.MultiRequests.RequestReference;

            // Assert
            Assert.Equal(4, actual.Count);
            Assert.Contains(actual, r => !r.ReferenceId.Except(new List<string>() { "a1", "s1", "r1" }).Any());
            Assert.Contains(actual, r => !r.ReferenceId.Except(new List<string>() { "a1", "s1", "r2" }).Any());
            Assert.Contains(actual, r => !r.ReferenceId.Except(new List<string>() { "a1", "s1", "r3" }).Any());
            Assert.Contains(actual, r => !r.ReferenceId.Except(new List<string>() { "a1", "s1", "r4" }).Any());
        }

        [Fact]
        public void CreateMultipleResourceCategory_ConsecutiveCategoryIdsCreated()
        {
            // Arrange
            int expectedCategoryCount = 5;
            List<string> expectedCategoryIds = new() { "r1", "r2", "r3", "r4", "r5" };

            List<CloudEvent> events = new() { _cloudEvent, _cloudEvent, _cloudEvent, _cloudEvent, _cloudEvent };

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

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEvent);
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
            // Arrange
            int expectedAttributeCount = 5;
            string expectedResourceId = "resourceInstanceId";

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEventWithResourceInstance);
            var actualResourceInstancedAttribute = actual.Attribute.FirstOrDefault(a => a.AttributeId.Equals("urn:altinn:resourceinstance"));

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Equal(expectedResourceId, actualResourceInstancedAttribute.Value);
        }
    }
}
