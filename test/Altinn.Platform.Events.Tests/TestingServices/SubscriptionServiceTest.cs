using System.Collections.Generic;
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

namespace Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="SubscriptionService"/>.
    /// </summary>
    public class SubscriptionServiceTest
    {
        private readonly Mock<ISubscriptionRepository> _repositoryMock = new();
/*
        [Fact]
        public async Task GetOrgSubscriptions_Two_Match()
        {
            SubscriptionService subscriptionService = GetSubscriptionService();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 2);
        }

        [Fact]
        public async Task GetOrgSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = GetSubscriptionService();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetSubscriptions_One_Match()
        {
            SubscriptionService subscriptionService = GetSubscriptionService();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/new-app",
                "/party/1337",
                null);
            Assert.True(result.Count == 1);
        }

        [Fact]
        public async Task GetSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = GetSubscriptionService();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetOrgSubscriptions_SendsIncludeInvalidFalseToRepository()
        {
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), false))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = GetSubscriptionService(repository: _repositoryMock.Object);

            await subscriptionService.GetSubscriptions("source", "party/1337", null);

            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task GetOrgSubscriptions_InvalidSourceUri()
        {
            SubscriptionService subscriptionService = GetSubscriptionService();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 0);
        }*/

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
        public async Task CreateSubscription_SubscriptionAlreadyExists_ReturnExisting()
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
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765", "serviceowner"));

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync(subscription);

            SubscriptionService subscriptionService = 
                GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            // Act
            var result = await subscriptionService.CreateSubscription(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task CreateSubscription_SubscriptionNotFound_ReturnNew()
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
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "87364765", "serviceowner"));

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync((Subscription)null);

            _repositoryMock.Setup(
                s => s.CreateSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId)))
                .ReturnsAsync(subscription);

            SubscriptionService subscriptionService =
                GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            // Act
            var result = await subscriptionService.CreateSubscription(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        private static SubscriptionService GetSubscriptionService(
            ISubscriptionRepository repository = null, 
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            return new SubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                new QueueServiceMock(),
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object,
                new Mock<IProfile>().Object,
                new Mock<IAuthorization>().Object,
                new Mock<IRegisterService>().Object);
        }
    }
}
