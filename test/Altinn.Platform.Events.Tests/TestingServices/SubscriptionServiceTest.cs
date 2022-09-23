using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
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
        private readonly Mock<ISubscriptionRepository> _repoMock;

        public SubscriptionServiceTest()
        {
            _repoMock = new Mock<ISubscriptionRepository>();
        }

        [Fact]
        public async Task GetOrgSubscriptions_Two_Match()
        {
            SubscriptionService subscriptionService = GetServiceForTest();
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 2);
        }

        [Fact]
        public async Task GetOrgSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = GetServiceForTest();
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetSubscriptions_One_Match()
        {
            SubscriptionService subscriptionService = GetServiceForTest();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/new-app",
                "/party/1337",
                null);
            Assert.True(result.Count == 1);
        }

        [Fact]
        public async Task GetSubscriptions_Zero_Match()
        {
            SubscriptionService subscriptionService = GetServiceForTest();
            List<Subscription> result = await subscriptionService.GetSubscriptions(
                "https://ttd.apps.altinn.no/ttd/endring-av-navn-v1",
                "/party/1337",
                null);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
        {
            _repoMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = GetServiceForTest(repository: _repoMock.Object);

            await subscriptionService.GetAllSubscriptions("/org/ttd");

            _repoMock.VerifyAll();
        }

        [Fact]
        public async Task GetOrgSubscriptions_SendsIncludeInvalidFalseToRepository()
        {
            _repoMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), false))
                .ReturnsAsync(new List<Subscription>());

            SubscriptionService subscriptionService = GetServiceForTest(repository: _repoMock.Object);

            await subscriptionService.GetOrgSubscriptions("source", "party/1337", null);

            _repoMock.VerifyAll();
        }

        [Fact]
        public async Task GetOrgSubscriptions_InvalidSourceUri()
        {
            SubscriptionService subscriptionService = GetServiceForTest();
            List<Subscription> result = await subscriptionService.GetOrgSubscriptions(
                "ttd/endring-av-navn-v2",
                "/party/1337",
                "app.instance.process.completed");
            Assert.True(result.Count == 0);
        }

        private SubscriptionService GetServiceForTest(ISubscriptionRepository repository = null)
        {
            return new SubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                new QueueServiceMock(),
                new Mock<IClaimsPrincipalProvider>().Object,
                new Mock<IProfile>().Object,
                new Mock<IAuthorization>().Object,
                new Mock<IRegisterService>().Object);
        }
    }
}
