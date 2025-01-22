using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Utils;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="AppSubscriptionService"/>.
    /// </summary>
    public class AppSubscriptionServiceTest
    {
        private readonly Mock<IAuthorization> _authTrueMock;
        private readonly Mock<IAuthorization> _authFalseMock;

        public AppSubscriptionServiceTest()
        {
            _authTrueMock = new();
            _authTrueMock
                .Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
                .ReturnsAsync(true);

            _authFalseMock = new();
            _authFalseMock
                .Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
                .ReturnsAsync(false);
        }

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
                AlternativeSubjectFilter = "/organisation/897069631"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.Is<string>(s => s.Equals("897069631")), It.IsAny<string>()))
                .ReturnsAsync(500700);

            var sut = GetAppSubscriptionService(
                register: registerMock.Object,
                authorization: _authTrueMock.Object);

            // Act
            (Subscription actual, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.Equal(expectedSubjectFilter, actual.SubjectFilter);
            Assert.Equal("/user/1337", actual.Consumer);
            Assert.Equal("/user/1337", actual.CreatedBy);
        }

        [Fact]
        public async Task CreateSubscription_PersonAsAlternativeSubject_SubjectFilterAndResourceFilterPopulated()
        {
            // Arrange
            string expectedSubjectFilter = "/party/1337";
            string expectedResourceFilter = "urn:altinn:resource:app_ttd_apps-test";
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

            var sut = GetAppSubscriptionService(
                register: registerMock.Object,
                authorization: _authTrueMock.Object);

            // Act
            (Subscription actual, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.Equal(expectedSubjectFilter, actual.SubjectFilter);
            Assert.Equal(expectedResourceFilter, actual.ResourceFilter);
        }

        [Fact]
        public async Task CreateSubscription_NoSourceFilter_SubscriptionCreated()
        {
            // Arrange
            string expectedSubjectFilter = "/party/1337";
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
                AlternativeSubjectFilter = "/person/01039012345",
                ResourceFilter = "urn:altinn:resource:app_ttd_apps-test"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s.Equals("01039012345"))))
                .ReturnsAsync(1337);

            var sut = GetAppSubscriptionService(
                register: registerMock.Object,
                authorization: _authTrueMock.Object);

            // Act
            (Subscription actual, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.NotNull(actual);
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
        public async Task CreateSubscription_InvalidSubjectForConsumer_ReturnsError()
        {
            // Arrange
            string expectedErrorMessage = "A valid subject to the authenticated identity is required";
            int expectedErrorCode = 400;
            int subscriptionId = 1337;

            var subs = new Subscription
            {
                Id = subscriptionId,
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
        public async Task CreateSubscription_InvalidAppSourceFilter_ReturnsError()
        {
            // Arrange
            string expectedErrorMessage = "A valid app id is required in source filter {environment}/{org}/{app}";
            int expectedErrorCode = 400;

            var subs = new Subscription
            {
                SourceFilter = new Uri("https://skd.apps.altinn.no/skd/mva-melding/instances/1337"),
                AlternativeSubjectFilter = "/person/01039012345"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s.Equals("01039012345"))))
                    .ReturnsAsync(1337);

            var sut = GetAppSubscriptionService(
                register: registerMock.Object);

            // Act
            (Subscription _, ServiceError actual) = await sut.CreateSubscription(subs);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
            Assert.Equal(expectedErrorCode, actual.ErrorCode);
        }

        [Fact]
        public async Task CreateSubscription_NonMatchingResourceAndSourceExistingResource_ReturnsError()
        {
            // Arrange
            string expectedErrorMessage = "Provided resource filter and source filter are not compatible";
            int expectedErrorCode = 400;
            var subs = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:app_skd_skatt-melding",
                SourceFilter = new Uri("https://skd.apps.altinn.no/skd/mva-melding"),
                AlternativeSubjectFilter = "/person/01039012345"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s.Equals("01039012345"))))
                    .ReturnsAsync(1337);

            var sut = GetAppSubscriptionService();

            // Act
            (Subscription _, ServiceError actual) = await sut.CreateSubscription(subs);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
            Assert.Equal(expectedErrorCode, actual.ErrorCode);
        }

        [Fact]
        public async Task CreateSubscription_ExistingResource_ResourceUnchanged()
        {
            // Arrange
            string expected = "urn:altinn:resource:app_skd_mva-melding";

            var subs = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:app_skd_mva-melding",
                SourceFilter = new Uri("https://skd.apps.altinn.no/skd/mva-melding"),
                AlternativeSubjectFilter = "/person/01039012345"
            };

            Mock<IRegisterService> registerMock = new();
            registerMock
                .Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s.Equals("01039012345"))))
                    .ReturnsAsync(1337);

            var sut = GetAppSubscriptionService(
                register: registerMock.Object,
                authorization: _authTrueMock.Object);

            // Act
            (Subscription subscription, ServiceError _) = await sut.CreateSubscription(subs);

            // Assert
            Assert.Equal(expected, subscription.ResourceFilter);
        }

        private static AppSubscriptionService GetAppSubscriptionService(
            IRegisterService register = null,
            IAuthorization authorization = null,
            ISubscriptionRepository repository = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            register ??= new RegisterServiceMock();

            authorization ??= new Mock<IAuthorization>().Object;

            if (claimsPrincipalProvider == null)
            {
                var mock = new Mock<IClaimsPrincipalProvider>();
                mock.Setup(
                    s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal(1337, 2));

                claimsPrincipalProvider = mock.Object;
            }

            return new AppSubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                authorization,
                register,
                new EventsQueueClientMock(),
                claimsPrincipalProvider);
        }
    }
}
