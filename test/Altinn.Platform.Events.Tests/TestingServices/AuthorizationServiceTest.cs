using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Models;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;
using Altinn.Platform.Register.Models;

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
        /// <summary>
        /// Test access to own event
        /// </summary>
        [Fact]
        public async Task AuthorizeConsumerForAltinnAppEvent_Self()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new();
            AuthorizationService authzHelper = new AuthorizationService(pdp, claimsPrincipalMock.Object);

            CloudEvent cloudEvent = new CloudEvent()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/user/1337");

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
            Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new();
            AuthorizationService authzHelper = new AuthorizationService(pdp, claimsPrincipalMock.Object);

            CloudEvent cloudEvent = new CloudEvent()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/org/ttd");

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
            Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new();
            AuthorizationService authzHelper = new AuthorizationService(pdp, claimsPrincipalMock.Object);

            CloudEvent cloudEvent = new CloudEvent()
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
        public async Task FilterAuthorizedRequests_PermitAll()
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
        public async Task FilterAuthorizedRequests_PermitOne()
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
    }
}
