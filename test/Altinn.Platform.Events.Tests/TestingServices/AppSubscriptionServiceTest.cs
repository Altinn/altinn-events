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

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="SubscriptionService"/>.
    /// </summary>
    public class AppSubscriptionServiceTest
    {
        private readonly Mock<ISubscriptionRepository> _repositoryMock = new();

        [Fact]
        public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
        {
            _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = GetAppSubscriptionService(repository: _repositoryMock.Object);

            await subscriptionService.GetAllSubscriptions("/org/ttd");

            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task CreateAppSubscription_SubscriptionAlreadyExists_ReturnExisting()
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

            AppSubscriptionService subscriptionService =
                GetAppSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            // Act
            var result = await subscriptionService.CreateSubscription(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task CreateAppSubscription_SubscriptionNotFound_ReturnNew()
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

            AppSubscriptionService subscriptionService =
                GetAppSubscriptionService(_repositoryMock.Object, claimsPrincipalProviderMock.Object);

            // Act
            var result = await subscriptionService.CreateSubscription(subscription);

            // Assert
            _repositoryMock.VerifyAll();
        }

        private static AppSubscriptionService GetAppSubscriptionService(
            ISubscriptionRepository repository = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            return new AppSubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                new EventsQueueClientMock(),
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object,
                new Mock<IProfile>().Object,
                new Mock<IAuthorization>().Object,
                new Mock<IRegisterService>().Object);
        }
    }
}
