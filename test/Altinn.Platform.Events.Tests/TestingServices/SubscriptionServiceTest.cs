using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Tests.Mocks;

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
        private readonly QueueServiceMock _queueServiceMock = new();
        private readonly SubscriptionRepositoryMock _subscriptionRepositoryMock = new();

        [Fact]
        public async Task GetOrgSubscriptions_Two_Match()
        {
            SubscriptionService subscriptionService = new SubscriptionService(_subscriptionRepositoryMock, _queueServiceMock);
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 2);
        }

        [Fact]
        public async Task GetOrgSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = new SubscriptionService(_subscriptionRepositoryMock, _queueServiceMock);
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetSubscriptions_One_Match()
        {
            SubscriptionService subscriptionService = new SubscriptionService(_subscriptionRepositoryMock, _queueServiceMock);
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/new-app",
                "/party/1337",
                null);
            Assert.True(result.Count == 1);
        }

        [Fact]
        public async Task GetSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = new SubscriptionService(_subscriptionRepositoryMock, _queueServiceMock);
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
        {
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = new SubscriptionService(_repositoryMock.Object, _queueServiceMock);

            await subscriptionService.GetAllSubscriptions("/org/ttd");

            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task GetOrgSubscriptions_SendsIncludeInvalidFalseToRepository()
        {
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), false))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = new SubscriptionService(_repositoryMock.Object, _queueServiceMock);

            await subscriptionService.GetOrgSubscriptions("source", "party/1337", null);

            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task GetOrgSubscriptions_InvalidSourceUri()
        {
            SubscriptionService subscriptionService = new SubscriptionService(_subscriptionRepositoryMock, _queueServiceMock);
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task CreateSubscription_SubscriptionAlreadyExists_ReturnExisting()
        {
            // Arrange
            int subscriptionId = 645187;
            Subscription subscription = new()
            {
                Id = subscriptionId
            };

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync(subscription);

            SubscriptionService subscriptionService = new(_repositoryMock.Object, _queueServiceMock);

            // Act
            Subscription result = await subscriptionService.CreateSubscription(subscription);

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
                Id = subscriptionId
            };

            _repositoryMock.Setup(
                s => s.FindSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId), CancellationToken.None))
                .ReturnsAsync((Subscription)null);

            _repositoryMock.Setup(
                s => s.CreateSubscription(
                    It.Is<Subscription>(p => p.Id == subscriptionId)))
                .ReturnsAsync(subscription);

            SubscriptionService subscriptionService = new(_repositoryMock.Object, _queueServiceMock);

            // Act
            Subscription result = await subscriptionService.CreateSubscription(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }
    }
}
