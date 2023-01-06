using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Profile.Models;
using Altinn.Platform.Register.Models;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="SubscriptionService"/>.
    /// </summary>
    public class AppSubscriptionServiceTest
    {
        [Fact]
        public async Task CreateSubscription_OrgAsAlternativeSubject_SubjectFilterPopulated()
        {
            // Arrange
            string expectedSubjectFilter = "/party/500700";
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                AlternativeSubjectFilter = "/org/897069631"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.Is<string>(s => s.Equals("897069631")), It.IsAny<string>()))
                .ReturnsAsync(500700);

            Mock<IProfile> profileMock = new();
            profileMock
                .Setup(p => p.GetUserProfile(It.IsAny<int>()))
                .ReturnsAsync(new UserProfile { Party = new Party { SSN = "01039012345" } });

            Mock<IAuthorization> authzMock = new();
            authzMock
                .Setup(a => a.AuthorizeConsumerForEventsSubcription(It.IsAny<Subscription>()))
                .ReturnsAsync(true);

            var sut = GetAppSubscriptionService(
                profile: profileMock.Object,
                register: registerMock.Object,
                authorization: authzMock.Object);

            // Act
            (Subscription actual, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.Equal(expectedSubjectFilter, actual.SubjectFilter);
        }

        [Fact]
        public async Task CreateSubscription_PersonAsAlternativeSubject_SubjectFilterPopulated()
        {
            // Arrange
            string expectedSubjectFilter = "/party/1337";
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                AlternativeSubjectFilter = "/person/01039012345"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s.Equals("01039012345"))))
                .ReturnsAsync(1337);

            Mock<IProfile> profileMock = new();
            profileMock
                .Setup(p => p.GetUserProfile(It.IsAny<int>()))
                .ReturnsAsync(new UserProfile { Party = new Party { SSN = "01039012345" } });

            var sut = GetAppSubscriptionService(
                profile: profileMock.Object,
                register: registerMock.Object);

            // Act
            (Subscription actual, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.Equal(expectedSubjectFilter, actual.SubjectFilter);
        }

        [Fact]
        public async Task CreateSubscription_NonExistentAlternativeSubject_ReturnsError()
        {
            // Arrange
            string expectedErrorMessage = "A valid subject to the authenticated identity is required";
            int expectedErrorCode = 400;
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                AlternativeSubjectFilter = "/person/14029112345"
            };

            var sut = GetAppSubscriptionService();

            // Act
            (Subscription _, ServiceError actual) = await sut.CreateSubscription(subs);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
            Assert.Equal(expectedErrorCode, actual.ErrorCode);
        }

        [Fact]
        public async Task CreateSubscription_NonMatchingConsumerProvided_ReturnsError()
        {
            // Arrange
            string expectedErrorMessage = "Not authorized to create a subscription on behalf of";
            int expectedErrorCode = 401;
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                SubjectFilter = "/party/1337",
                Consumer = "/party/2016"
            };

            var sut = GetAppSubscriptionService();

            // Act
            (Subscription _, ServiceError actual) = await sut.CreateSubscription(subs);

            // Assert
            Assert.NotNull(actual);
            Assert.StartsWith(expectedErrorMessage, actual.ErrorMessage);
            Assert.Equal(expectedErrorCode, actual.ErrorCode);
        }

        private static AppSubscriptionService GetAppSubscriptionService(
            IRegisterService register = null,
            IProfile profile = null,
            IAuthorization authorization = null,
            ISubscriptionRepository repository = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            if (register == null)
            {
                register = new RegisterServiceMock();
            }

            if (profile == null)
            {
                profile = new ProfileMockSI(register);
            }

            if (authorization == null)
            {
                authorization = new Mock<IAuthorization>().Object;
            }

            if (claimsPrincipalProvider == null)
            {
                var mock = new Mock<IClaimsPrincipalProvider>();
                mock.Setup(
                    s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal(1337, 2));

                claimsPrincipalProvider = mock.Object;
            }

            return new AppSubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                new EventsQueueClientMock(),
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object,
                profile,
                authorization,
                register);
        }
    }
}
