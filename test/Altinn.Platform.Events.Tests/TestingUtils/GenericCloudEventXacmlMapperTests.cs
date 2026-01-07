using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Altinn.Common.PEP.Constants;

using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Tests.Utils;

using CloudNative.CloudEvents;

using Xunit;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    public class GenericCloudEventXacmlMapperTests
    {
        private const string _subscribeActionType = "subscribe";
        private readonly CloudEvent _cloudEvent;
        private readonly CloudEvent _cloudEventWithResourceInstance;
        private readonly CloudEvent _cloudEventWithOrgNoSubject;
        private readonly CloudEvent _cloudEventWithPersonSubject;
        private readonly CloudEvent _cloudEventWithNoSubject;
        private readonly CloudEvent _cloudEventWithUnknownSubject;

        public GenericCloudEventXacmlMapperTests()
        {
            _cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            _cloudEvent["resource"] = "urn:altinn:resource:nbib.bokoversikt.api";

            _cloudEventWithOrgNoSubject = _cloudEvent.Clone();
            _cloudEventWithOrgNoSubject.Subject = "urn:altinn:organization:identifier-no:912345678";

            _cloudEventWithPersonSubject = _cloudEvent.Clone();
            _cloudEventWithPersonSubject.Subject = "urn:altinn:person:identifier-no:12345678901";

            _cloudEventWithNoSubject = _cloudEvent.Clone();
            _cloudEventWithNoSubject.Subject = null;

            _cloudEventWithUnknownSubject = _cloudEvent.Clone();
            _cloudEventWithUnknownSubject.Subject = "foobar";

            _cloudEventWithResourceInstance = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/person/16069412345",
                Source = new Uri("urn:isbn:1234567890")
            };

            _cloudEventWithResourceInstance["resource"] = "urn:altinn:resource:nbib.bokoversikt.api";
            _cloudEventWithResourceInstance["resourceinstance"] = "resourceInstanceId";
        }

        [Fact]
        public void CreateMultiDecisionRequest_ShouldReturnValidXACMLRequest_ForMultipleConsumers()
        {
            // Arrange
            List<string> consumers = ["/org/ttd", "/org/skd", "/person/16069412345"];

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequestForMultipleConsumers(_cloudEvent, consumers, _subscribeActionType);

            // Assert
            Assert.NotNull(actual);
            Assert.NotNull(actual.Request);
            Assert.NotEmpty(actual.Request.Action);
            Assert.NotEmpty(actual.Request.AccessSubject);
            Assert.Equal(3, actual.Request.AccessSubject.Count);
            Assert.NotEmpty(actual.Request.Resource);
            Assert.NotNull(actual.Request.MultiRequests);
            Assert.Equal(3, actual.Request.MultiRequests.RequestReference.Count);
            
            // Verify subject IDs are sequential
            var subjectIds = actual.Request.AccessSubject.Select(s => s.Id).ToList();
            Assert.Contains("s1", subjectIds);
            Assert.Contains("s2", subjectIds);
            Assert.Contains("s3", subjectIds);
            
            // Verify multi-request references include all subjects
            Assert.Contains(actual.Request.MultiRequests.RequestReference, r => !r.ReferenceId.Except(["a1", "s1", "r1"]).Any());
            Assert.Contains(actual.Request.MultiRequests.RequestReference, r => !r.ReferenceId.Except(["a1", "s2", "r1"]).Any());
            Assert.Contains(actual.Request.MultiRequests.RequestReference, r => !r.ReferenceId.Except(["a1", "s3", "r1"]).Any());
        }

        [Fact]
        public void CreateMultiDecisionRequest_AssertActionCategory()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);
            List<CloudEvent> events = new() { _cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, "subscribe", events).Request.Action;
            var actualAction = actual.First();
            var actualActionValue = actualAction.Attribute.Where(a => a.AttributeId.Equals(MatchAttributeIdentifiers.ActionId)).Select(a => a.Value).FirstOrDefault();

            Assert.Single(actual);
            Assert.Equal("a1", actualAction.Id);
            Assert.Equal("subscribe", actualActionValue);
        }

        [Fact]
        public void CreateMultiDecisionRequest_ClaimsPrincipal_AssertSubjectCategory()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);
            List<CloudEvent> events = new() { _cloudEvent };

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, "subscribe", events).Request.AccessSubject;
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
            var actual = GenericCloudEventXacmlMapper.CreateMultiDecisionRequest(user, "subscribe", events).Request.MultiRequests.RequestReference;

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
            var actualEventIdAttribute = actual.Attribute.Find(a => a.AttributeId.Equals("urn:altinn:event-id"));

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
            var actualResourceInstancedAttribute = actual.Attribute.Find(a => a.AttributeId.Equals(AltinnXacmlUrns.ResourceInstance));

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Equal(expectedResourceId, actualResourceInstancedAttribute.Value);
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithPersonSubject()
        {
            // Arrange
            int expectedAttributeCount = 5;

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEventWithPersonSubject);

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:person:identifier-no"));
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithOrgNoSubject()
        {
            // Arrange
            int expectedAttributeCount = 5;

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEventWithOrgNoSubject);

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
            Assert.Contains(actual.Attribute, a => a.AttributeId.Equals("urn:altinn:organization:identifier-no"));
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithNoSubject()
        {
            // Arrange
            int expectedAttributeCount = 4;

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEventWithNoSubject);

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
        }

        [Fact]
        public void CreateResourceCategory_CloudEventWithUnknownSubject()
        {
            // Arrange
            int expectedAttributeCount = 4;

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateResourceCategory(_cloudEventWithUnknownSubject);

            // Assert
            Assert.Equal(expectedAttributeCount, actual.Attribute.Count);
        }

        [Fact]
        public void CreateDecisionRequest_ClaimsPrincipalConsumer_AllCategoriesPopulated()
        {
            // Arrange
            ClaimsPrincipal user = PrincipalUtil.GetClaimsPrincipal(1337, 2);

            // Act
            var actual = GenericCloudEventXacmlMapper.CreateDecisionRequest(user, "subscribe", _cloudEvent);

            // Assert
            Assert.NotEmpty(actual.Request.Action);
            Assert.NotEmpty(actual.Request.AccessSubject);
            Assert.NotEmpty(actual.Request.Resource);
        }

        [Fact]
        public void CreateDecisionRequest_StringConsumer_AllCategoriesPopulated()
        {
            // Act
            var actual = GenericCloudEventXacmlMapper.CreateDecisionRequest("/org/ttd", "subscribe", _cloudEvent);

            // Assert
            Assert.NotEmpty(actual.Request.Action);
            Assert.NotEmpty(actual.Request.AccessSubject);
            Assert.NotEmpty(actual.Request.Resource);
        }
    }
}
