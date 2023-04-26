using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Tests to verify authorization helper
    /// </summary>
    public class AuthorizationServiceTest
    {
        private readonly CloudEvent _cloudEvent;
        private readonly Mock<IClaimsPrincipalProvider> _principalMock = new();

        public AuthorizationServiceTest()
        {
            _cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "system.event.occurred",
                Subject = "/party/1337",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd")
            };
            _cloudEvent["resource"] = "urn:altinn:resource:ttd.endring-av-navn-v2";

            _principalMock
                   .Setup(p => p.GetUser())
                   .Returns(PrincipalUtil.GetClaimsPrincipal(12345, 3));
        }

        /// <summary>
        /// Test access to own event
        /// </summary>
        [Fact]
        public async Task AuthorizeConsumerForAltinnAppEvent_Self()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp, _principalMock.Object);

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(_cloudEvent, "/user/1337");

            // Assert.
            Assert.True(result);
        }

        /// <summary>
        /// Test access to own event
        /// </summary>
        [Fact]
        public async Task AuthorizeConsumerForAltinnAppEvent_OrgAccessToEventForUser()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp, _principalMock.Object);

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(_cloudEvent, "/org/ttd");

            // Assert.
            Assert.True(result);
        }

        /// <summary>
        /// Test access to event for user for org.Not authorized
        /// </summary>
        [Fact]
        public async Task AuthorizeConsumerForAltinnAppEvent_OrgAccessToEventForUserNotAuthorized()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp, _principalMock.Object);

            CloudEvent cloudEvent = new()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/org/nav");

            // Assert.
            Assert.False(result);
        }

        [Fact]
        public async Task AuthorizeConsumerForGenericEvent_PdpIsCalledWithXacmlRequestRoot()
        {
            // Arrange
            Mock<IPDP> pdpMock = GetPDPMockWithRespose("Permit");

            // Act            
            var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object);

            bool actual = await sut.AuthorizeConsumerForGenericEvent(_cloudEvent, "/org/ttd/");

            // Assert
            pdpMock.VerifyAll();
            Assert.True(actual);
        }

        [Fact]
        public void FilterAuthorizedRequests_PermitAll()
        {
            // Arrange
            ClaimsPrincipal consumer = PrincipalUtil.GetClaimsPrincipal(12345, 3);
            string testCase = "permit_all";
            List<CloudEvent> cloudEvents = TestdataUtil.GetXacmlRequestCloudEventList();
            XacmlJsonResponse response = TestdataUtil.GetXacmlJsonResponse(testCase);

            // Act            
            List<CloudEvent> actual = AuthorizationService.FilterAuthorizedRequests(cloudEvents, consumer, response);

            // Assert
            Assert.NotEmpty(actual);
            Assert.Equal(2, actual.Count);
        }

        [Fact]
        public void FilterAuthorizedRequests_PermitOne()
        {
            // Arrange
            ClaimsPrincipal consumer = PrincipalUtil.GetClaimsPrincipal(12345, 3);
            string testCase = "permit_one";
            List<CloudEvent> cloudEvents = TestdataUtil.GetXacmlRequestCloudEventList();
            XacmlJsonResponse response = TestdataUtil.GetXacmlJsonResponse(testCase);

            // Act            
            List<CloudEvent> actual = AuthorizationService.FilterAuthorizedRequests(cloudEvents, consumer, response);

            // Assert
            Assert.Single(actual);
        }

        [Fact]
        public async Task AuthorizePublishEvent_PermitResponse_ReturnsTrue()
        {
            // Arrange
            Mock<IPDP> pdpMock = GetPDPMockWithRespose("Permit");

            // Act            
            var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object);

            bool actual = await sut.AuthorizePublishEvent(_cloudEvent);

            // Assert
            pdpMock.VerifyAll();
            Assert.True(actual);
        }

        [Fact]
        public async Task AuthorizePublishEvent_DenyResponse_ReturnsFalse()
        {
            // Arrange
            Mock<IPDP> pdpMock = GetPDPMockWithRespose("Deny");

            // Act            
            var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object);

            bool actual = await sut.AuthorizePublishEvent(_cloudEvent);

            // Assert
            pdpMock.VerifyAll();
            Assert.False(actual);
        }

        [Fact]
        public async Task AuthorizePublishEvent_IndeterminateResponse_ReturnsFalse()
        {
            // Arrange
            Mock<IPDP> pdpMock = GetPDPMockWithRespose("Indeterminate");

            // Act            
            var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object);

            bool actual = await sut.AuthorizePublishEvent(_cloudEvent);

            // Assert
            Assert.False(actual);
        }

        private static Mock<IPDP> GetPDPMockWithRespose(string decision)
        {
            var pdpMock = new Mock<IPDP>();
            pdpMock
                .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
                .ReturnsAsync(new XacmlJsonResponse
                {
                    Response = new List<XacmlJsonResult>()
                    {
                        new XacmlJsonResult
                        {
                            Decision = decision
                        }
                    }
                });

            return pdpMock;
        }
    }
}
