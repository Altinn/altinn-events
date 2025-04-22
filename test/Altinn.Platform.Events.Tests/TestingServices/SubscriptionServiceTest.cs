using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.AccessManagement.Core.Models;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
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
        public async Task CompleteSubscriptionCreation_SubscriptionAlreadyExists_ReturnExisting()
        {
            // Arrange
            int subscriptionId = 645187;
            Subscription subscription = new()
            {
                Id = subscriptionId,
                SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
            };

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            _repositoryMock.Setup(s => s.FindSubscription(It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None)).ReturnsAsync(subscription);

            SubscriptionService subscriptionService = GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

            // Act
            await subscriptionService.CompleteSubscriptionCreation(subscription);

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
                SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
            };

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            _repositoryMock.Setup(s => s.FindSubscription(It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None)).ReturnsAsync((Subscription)null);

            _repositoryMock.Setup(s => s.CreateSubscription(It.Is<Subscription>(p => p.Id == subscriptionId))).ReturnsAsync(subscription);

            Mock<IEventsQueueClient> queueMock = new();

            queueMock.Setup(q => q.EnqueueSubscriptionValidation(It.Is<string>(s => CheckSubscriptionId(s, 645187))));

            // Act
            SubscriptionService subscriptionService = GetSubscriptionService(_repositoryMock.Object, queueMock: queueMock.Object, claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

            await subscriptionService.CompleteSubscriptionCreation(subscription);

            // Assert
            _repositoryMock.VerifyAll();
            queueMock.VerifyAll();
        }

        [Fact]
        public async Task CreateSubscription_WithoutAccess_ReturnsForbiddenError()
        {
            // Arrange 
            string expectedErrorMessage = "Not authorized to create a subscription for resource urn:altinn:resource:some-service.";

            var input = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://automated.com"),
            };

            var sut = GetSubscriptionService(authorizationDecision: false);

            // Act
            (var _, ServiceError actual) = await sut.CompleteSubscriptionCreation(input);

            // Assert
            Assert.Equal(403, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
        {
            // Arrange
            Mock<IClaimsPrincipalProvider> userProvider = new Mock<IClaimsPrincipalProvider>();

            // The organisation number is not used by the logic when an org claim exists.
            userProvider.Setup(u => u.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "na"));
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true)).ReturnsAsync([]);

            SubscriptionService subscriptionService = GetSubscriptionService(repository: _repositoryMock.Object, claimsPrincipalProvider: userProvider.Object);

            // Act
            await subscriptionService.GetAllSubscriptions();

            // Assert
            _repositoryMock.VerifyAll();
        }

        [Theory]
        [InlineData(EntityType.Org, "ttd", "/org/ttd")]
        [InlineData(EntityType.Organisation, "987654321", "/organisation/987654321")]
        [InlineData(EntityType.User, "1406840", "/user/1406840")]
        [InlineData(EntityType.SystemUser, "f02a9454-36ad-4ec9-8aa3-531449c5ae7f", "/systemuser/f02a9454-36ad-4ec9-8aa3-531449c5ae7f")]
        public void GetEntityFromPrincipal(EntityType entityType, string entityKeyValue, string expectedEntity)
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

                case EntityType.SystemUser:
                    principal = PrincipalUtil.GetSystemUserPrincipal("random_system_identifier", entityKeyValue, "random_org_cliam_identifier", 3);
                    break;
            }

            Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
            claimsPrincipalProviderMock.Setup(s => s.GetUser()).Returns(principal);

            SubscriptionService subscriptionService = GetSubscriptionService(claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

            // Act
            string actualEntity = subscriptionService.GetEntityFromPrincipal();

            // Assert
            Assert.Equal(expectedEntity, actualEntity);
        }

        [Fact]
        public void GetSystemUserId_SystemUserIsNull_ReturnsNull()
        {
            // Arrange
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = claimsPrincipal.GetSystemUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetSystemUserId_SystemUserIdIsEmpty_ReturnsNull()
        {
            // Arrange
            var systemUserClaim = new SystemUserClaim { Systemuser_id = [] };
            var claimsPrincipal = CreateClaimsPrincipal(systemUserClaim);

            // Act
            var result = claimsPrincipal.GetSystemUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetSystemUserId_SystemUserIdIsInvalidGuid_ReturnsNull()
        {
            // Arrange
            var invalidGuid = "invalid-guid";
            var systemUserClaim = new SystemUserClaim { Systemuser_id = [invalidGuid] };
            var claimsPrincipal = CreateClaimsPrincipal(systemUserClaim);

            // Act
            var result = claimsPrincipal.GetSystemUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetSystemUserId_SystemUserIdIsNull_ReturnsNull()
        {
            // Arrange
            var systemUserClaim = new SystemUserClaim { Systemuser_id = null };
            var claimsPrincipal = CreateClaimsPrincipal(systemUserClaim);

            // Act
            var result = claimsPrincipal.GetSystemUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetSystemUserId_SystemUserIdIsValidGuid_ReturnsGuid()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();
            var systemUserClaim = new SystemUserClaim { Systemuser_id = [validGuid] };
            var claimsPrincipal = CreateClaimsPrincipal(systemUserClaim);

            // Act
            var result = claimsPrincipal.GetSystemUserId();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Guid.Parse(validGuid), result);
        }

        private static bool CheckSubscriptionId(string serializedSubscription, int expectedId)
        {
            var subscription = JsonSerializer.Deserialize<Subscription>(serializedSubscription);
            return expectedId == subscription.Id;
        }

        private static ClaimsPrincipal CreateClaimsPrincipal(SystemUserClaim systemUserClaim)
        {
            var claims = new List<Claim>
            {
                new("authorization_details", JsonSerializer.Serialize(systemUserClaim))
            };

            var identity = new ClaimsIdentity(claims);
            return new ClaimsPrincipal(identity);
        }

        private static SubscriptionService GetSubscriptionService(
            ISubscriptionRepository repository = null,
            bool authorizationDecision = true,
            IEventsQueueClient queueMock = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            var authoriationMock = new Mock<IAuthorization>();
            authoriationMock
                .Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
                .ReturnsAsync(authorizationDecision);

            return new SubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                authoriationMock.Object,
                queueMock ?? new Mock<IEventsQueueClient>().Object,
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object);
        }

        public enum EntityType
        {
            User,
            Org,
            Organisation,
            SystemUser
        }
    }
}
