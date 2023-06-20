using System;
using System.Threading;
using System.Threading.Tasks;

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
    /// A collection of tests related to <see cref="GenericSubscriptionService"/>.
    /// </summary>
    public class GenericSubscriptionServiceTest
    {
        [Theory]
        [InlineData("https://fantastiske-hundepassere.no/events", "/dog/bruno")]
        [InlineData("https://fantastiske-hundepassere.no", "/dog/bruno")]
        public async Task CreateSubscription_ValidAndAuthorizedSubscription_ReturnsNewSubscription(string endpoint, string subjectFilter)
        {
            // Arrange 
            var input = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:some-service",
                SubjectFilter = subjectFilter,
                EndPoint = new Uri(endpoint),
            };

            Mock<ISubscriptionRepository> repoMock = new();
            var sut = GetGenericSubscriptionService(repoMock);

            // Act
            (var actual, ServiceError _) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal("/org/ttd", actual.CreatedBy);
            Assert.Equal("/org/ttd", actual.Consumer);
            repoMock.VerifyAll();
        }

        [Fact]
        public async Task CreateSubscription_AlternaticSubjectFilterProvided_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "AlternativeSubject is not supported for subscriptions on this resource.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                AlternativeSubjectFilter = "/object/123456"
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task CreateSubscription_InvalidUrnProvidedAsSourceFilter_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "Source filter is not supported for subscriptions on this resource.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                SourceFilter = new Uri("telnet://ole:qwerty@altinn.no:45432/")
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task CreateSubscription_MissingResourceFilter_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "Resource filter is required.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task CreateSubscription_Unauthorized_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "Not authorized to create a subscription for resource urn:altinn:resource:some-service and subject filter: .";

            var input = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://automated.com"),
            };

            var sut = GetGenericSubscriptionService(isAuthorized: false);

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(401, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        private static GenericSubscriptionService GetGenericSubscriptionService(Mock<ISubscriptionRepository> repoMock = null, bool isAuthorized = true)
        {
            var claimsProviderMock = new Mock<IClaimsPrincipalProvider>();
            claimsProviderMock.Setup(
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "1234567892"));

            var authorizationMock = new Mock<IAuthorization>();
            authorizationMock.Setup(
                a => a.AuthorizeConsumerForEventsSubcription(It.IsAny<Subscription>()))
                .ReturnsAsync(isAuthorized);

            if (repoMock == null)
            {
                repoMock = new();
            }

            repoMock
                 .Setup(r => r.FindSubscription(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Subscription)null);

            repoMock
                .Setup(r => r.CreateSubscription(It.IsAny<Subscription>(), It.IsAny<string>()))
                        .ReturnsAsync((Subscription s, string _) =>
                        {
                            s.Id = new Random().Next(1, int.MaxValue);
                            s.Created = DateTime.Now;

                            return s;
                        });

            return new GenericSubscriptionService(
                repoMock.Object,
                new Mock<IRegisterService>().Object,
                authorizationMock.Object,
                new EventsQueueClientMock(),
                claimsProviderMock.Object)
            {
            };
        }
    }
}
