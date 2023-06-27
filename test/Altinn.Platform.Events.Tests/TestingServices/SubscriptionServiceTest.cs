using System.Collections.Generic;
using System.Security.Claims;
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
    /// A collection of tests related to <see cref="SubscriptionService"/>.
    /// </summary>
    public class SubscriptionServiceTest
    {
        private readonly Mock<ISubscriptionRepository> _repositoryMock = new();

        [Fact]
        public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
        {
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = GetSubscriptionService(repository: _repositoryMock.Object);

            await subscriptionService.GetAllSubscriptions("/org/ttd");

            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task CompleteSubscriptionCreation_SubscriptionAlreadyExists_ReturnExisting()
        {
            // Arrange
            int subscriptionId = 645187;
            Subscription subscription = new()
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
            };

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync(subscription);

            SubscriptionService subscriptionService =
                GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            // Act
            var result = await subscriptionService.CompleteSubscriptionCreation(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task CompleteSubscriptionCreation_SubscriptionNotFound_ReturnNew()
        {
            // Arrange
            int subscriptionId = 645187;
            Subscription subscription = new()
            {
                Id = subscriptionId,
                SourceFilter = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
            };

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync((Subscription)null);

            _repositoryMock.Setup(
                s => s.CreateSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId),
                    It.Is<string>(s => s.Equals("03E4D9CA0902493533E9C62AB437EF50"))))
                .ReturnsAsync(subscription);

            // Act
            SubscriptionService subscriptionService =
                  GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            var result = await subscriptionService.CompleteSubscriptionCreation(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        [Theory]
        [InlineData(EntityType.Org, "ttd", "/org/ttd")]
        [InlineData(EntityType.Organisation, "987654321", "/party/1337")]
        [InlineData(EntityType.User, "1406840", "/user/1406840")]
        public async Task GetEntityFromPrincipal(EntityType entityType, string entityKeyValue, string expectedEntity)
        {
            // Arrange
            ClaimsPrincipal principal = null;

            switch (entityType)
            {
                case EntityType.User:
                    principal = PrincipalUtil.GetClaimsPrincipal(int.Parse(entityKeyValue), 2);
                    break;
                case EntityType.Org:
                    principal = PrincipalUtil.GetClaimsPrincipal(entityKeyValue, "87364765");
                    break;
                case EntityType.Organisation:
                    principal = PrincipalUtil.GetClaimsPrincipal(entityKeyValue);
                    break;
            }

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(
                s => s.GetUser()).Returns(principal);

            Mock<IRegisterService> registerMock = new();
            registerMock.Setup(r => r.PartyLookup(It.IsAny<string>(), It.Is<string>(s => s == null)))
                .ReturnsAsync(1337);

            SubscriptionService subscriptionService =
             GetSubscriptionService(null, claimsPrincipalProviderMock.Object, registerMock.Object);

            // Act
            var actualEntity = await subscriptionService.GetEntityFromPrincipal();

            // Assert
            Assert.Equal(expectedEntity, actualEntity);
        }

        public enum EntityType
        {
            User,
            Org,
            Organisation
        }

        private static SubscriptionService GetSubscriptionService(
            ISubscriptionRepository repository = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null,
            IRegisterService registerMock = null,
            bool authorizationDecision = true)
        {
            var authoriationMock = new Mock<IAuthorization>();
            authoriationMock
                .Setup(a => a.AuthorizeConsumerForEventsSubcription(It.IsAny<Subscription>()))
                .ReturnsAsync(authorizationDecision);

            return new SubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                registerMock ?? new Mock<IRegisterService>().Object,
                authoriationMock.Object,
                new EventsQueueClientMock(),
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object);
        }
    }
}
