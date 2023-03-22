using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
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
        private readonly AuthorizationService _sut;

        public AuthorizationServiceTest()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new Mock<IClaimsPrincipalProvider>();
            _sut = new AuthorizationService(pdp, claimsPrincipalMock.Object);
        }

        /// <summary>
        /// Test access to own event
        /// </summary>
        [Fact]
        public async Task AuthorizeAccessToEventForSelf()
        {
            CloudEvent cloudEvent = new CloudEvent()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await _sut.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/user/1337");

            // Assert.
            Assert.True(result);
        }

        /// <summary>
        /// Test access to own event
        /// </summary>
        [Fact]
        public async Task AuthorizeOrgAccessToEventForUser()
        {
            CloudEvent cloudEvent = new CloudEvent()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await _sut.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/org/ttd");

            // Assert.
            Assert.True(result);
        }

        /// <summary>
        /// Test access to event for user for org.Not authorized
        /// </summary>
        [Fact]
        public async Task AuthorizeOrgAccessToEventForUserNotAuthorized()
        {
            CloudEvent cloudEvent = new CloudEvent()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await _sut.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/org/nav");

            // Assert.
            Assert.False(result);
        }
    }
}
