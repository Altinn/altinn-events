using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.AccessManagement.Core.Models;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Wolverine;
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

            _repositoryMock.Setup(s => s.FindSubscription(It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync((Subscription)null);

            _repositoryMock.Setup(s => s.CreateSubscription(It.Is<Subscription>(p => p.Id == subscriptionId)))
                .ReturnsAsync(subscription);

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.PublishAsync(
                It.Is<ValidateSubscriptionCommand>(cmd => cmd.Subscription.Id == subscriptionId), 
                It.IsAny<DeliveryOptions>()))
                .Returns(ValueTask.CompletedTask)
                .Verifiable();

            // Act
            SubscriptionService subscriptionService = GetSubscriptionService(
                _repositoryMock.Object,
                messageBus: messageBusMock.Object,
                claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

            await subscriptionService.CompleteSubscriptionCreation(subscription);

            // Assert
            _repositoryMock.VerifyAll();
            messageBusMock.VerifyAll();
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

        [Fact]
        public async Task DeleteSubscription_SubscriptionNotFound_Returns404()
        {
            // Arrange
            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(It.IsAny<int>())).ReturnsAsync((Subscription)null);

            SubscriptionService sut = GetSubscriptionService(repository: repositoryMock.Object);

            // Act
            ServiceError error = await sut.DeleteSubscription(999);

            // Assert
            Assert.NotNull(error);
            Assert.Equal(404, error.ErrorCode);
        }

        [Fact]
        public async Task DeleteSubscription_NotAuthorized_Returns403()
        {
            // Arrange
            var subscription = new Subscription { Id = 1, CreatedBy = "/org/skd" };

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(1)).ReturnsAsync(subscription);

            Mock<IClaimsPrincipalProvider> claimsMock = new();
            claimsMock.Setup(s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            SubscriptionService sut = GetSubscriptionService(repository: repositoryMock.Object, claimsPrincipalProvider: claimsMock.Object);

            // Act
            ServiceError error = await sut.DeleteSubscription(1);

            // Assert
            Assert.NotNull(error);
            Assert.Equal(403, error.ErrorCode);
        }

        [Fact]
        public async Task GetSubscription_SubscriptionNotFound_Returns404()
        {
            // Arrange
            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(It.IsAny<int>())).ReturnsAsync((Subscription)null);

            SubscriptionService sut = GetSubscriptionService(repository: repositoryMock.Object);

            // Act
            (Subscription result, ServiceError error) = await sut.GetSubscription(999);

            // Assert
            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Equal(404, error.ErrorCode);
        }

        [Fact]
        public async Task GetSubscription_NotAuthorized_Returns403()
        {
            // Arrange
            var subscription = new Subscription { Id = 1, CreatedBy = "/org/skd" };

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(1)).ReturnsAsync(subscription);

            Mock<IClaimsPrincipalProvider> claimsMock = new();
            claimsMock.Setup(s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            SubscriptionService sut = GetSubscriptionService(repository: repositoryMock.Object, claimsPrincipalProvider: claimsMock.Object);

            // Act
            (Subscription result, ServiceError error) = await sut.GetSubscription(1);

            // Assert
            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Equal(403, error.ErrorCode);
        }

        [Fact]
        public async Task SetValidSubscription_SubscriptionNotFound_Returns404()
        {
            // Arrange
            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(It.IsAny<int>())).ReturnsAsync((Subscription)null);

            SubscriptionService sut = GetSubscriptionService(repository: repositoryMock.Object);

            // Act
            (Subscription result, ServiceError error) = await sut.SetValidSubscription(999);

            // Assert
            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Equal(404, error.ErrorCode);
        }

        [Fact]
        public async Task SendAndValidate_SetValidReturns404_LogsError()
        {
            // Arrange
            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(It.IsAny<int>())).ReturnsAsync((Subscription)null);

            Mock<IWebhookService> webhookMock = new();
            webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Mock<ILogger<SubscriptionService>> loggerMock = new();

            var subscription = new Subscription
            {
                Id = 42,
                Consumer = "/org/ttd",
                EndPoint = new Uri("https://example.com/webhook")
            };

            SubscriptionService sut = GetSubscriptionService(
                repository: repositoryMock.Object,
                webhookService: webhookMock.Object,
                logger: loggerMock.Object);

            // Act
            await sut.SendAndValidate(subscription, CancellationToken.None);

            // Assert
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task SendAndValidate_SetValidThrows_ThrowsInvalidOperationException()
        {
            // Arrange
            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.GetSubscription(It.IsAny<int>())).ThrowsAsync(new Exception("DB error"));

            Mock<IWebhookService> webhookMock = new();
            webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var subscription = new Subscription
            {
                Id = 42,
                Consumer = "/org/ttd",
                EndPoint = new Uri("https://example.com/webhook")
            };

            SubscriptionService sut = GetSubscriptionService(
                repository: repositoryMock.Object,
                webhookService: webhookMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAndValidate(subscription, CancellationToken.None));
        }

        [Fact]
        public void GetEntityFromPrincipal_NoClaims_ReturnsNull()
        {
            // Arrange
            Mock<IClaimsPrincipalProvider> claimsMock = new();
            claimsMock.Setup(s => s.GetUser()).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

            SubscriptionService sut = GetSubscriptionService(claimsPrincipalProvider: claimsMock.Object);

            // Act
            string result = sut.GetEntityFromPrincipal();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task PublishSubscriptionValidation_ServiceBusDisabled_QueueFails_Throws()
        {
            // Arrange
            int subscriptionId = 100;
            var subscription = new Subscription
            {
                Id = subscriptionId,
                SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
            };

            Mock<IClaimsPrincipalProvider> claimsMock = new();
            claimsMock.Setup(s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765"));

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueSubscriptionValidation(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = false, Exception = new Exception("Queue failed") });

            SubscriptionService sut = GetSubscriptionService(
                claimsPrincipalProvider: claimsMock.Object,
                queueClient: queueMock.Object,
                wolverineSettings: new WolverineSettings { EnableServiceBus = false });

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => sut.CompleteSubscriptionCreation(subscription));
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
            IMessageBus messageBus = null,
            IEventsQueueClient queueClient = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null,
            WolverineSettings wolverineSettings = null,
            IWebhookService webhookService = null,
            ILogger<SubscriptionService> logger = null)
        {
            var authoriationMock = new Mock<IAuthorization>();
            authoriationMock
                .Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
                .ReturnsAsync(authorizationDecision);

            var platformSettings = new PlatformSettings
            {
                ApiEventsEndpoint = "https://platform.altinn.no/events/api/v1/"
            };

            return new SubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                authoriationMock.Object,
                messageBus ?? new Mock<IMessageBus>().Object,
                queueClient ?? new Mock<IEventsQueueClient>().Object,
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object,
                Options.Create(platformSettings),
                Options.Create(wolverineSettings ?? new WolverineSettings { EnableServiceBus = true }),
                webhookService ?? new Mock<IWebhookService>().Object,
                logger ?? new Mock<ILogger<SubscriptionService>>().Object);
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
