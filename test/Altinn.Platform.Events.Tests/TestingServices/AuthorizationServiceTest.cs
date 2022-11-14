using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services;

using Altinn.Platform.Events.UnitTest.Mocks;

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
        public async Task AuthorizeAccessToEventForSelf()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp);

            CloudEventOld cloudEvent = new CloudEventOld()
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
        public async Task AuthorizeOrgAccessToEventForUser()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp);

            CloudEventOld cloudEvent = new CloudEventOld()
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
        public async Task AuthorizeOrgAccessToEventForUserNotAuthorized()
        {
            PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
            AuthorizationService authzHelper = new AuthorizationService(pdp);

            CloudEventOld cloudEvent = new CloudEventOld()
            {
                Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                Subject = "/party/1337"
            };

            // Act
            bool result = await authzHelper.AuthorizeConsumerForAltinnAppEvent(cloudEvent, "/org/nav");

            // Assert.
            Assert.False(result);
        }
    }
}
