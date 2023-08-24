using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingDecorators
{
    public class SubscriptionRepositoryCacheDecoratorTest
    {
        [Fact]
        public async Task GetSubscriptions_CachePopulated_ServiceNotCalled()
        {
            // Arrange
            var repositoryMock = new Mock<ISubscriptionRepository>();
            (IMemoryCache cache, SubscriptionRepositoryCachingDecorator sut) = GetCacheAndDecorator(repositoryMock);

            cache.Set(
               "subscription:re:resourcesu:testsubjectty:event.automated",
               new List<Subscription> { new Subscription { Id = 1337 } });

            // Act 
            var actual = await sut.GetSubscriptions(
                "resource",
                "testsubject",
                "event.automated",
                CancellationToken.None);

            // Assert
            repositoryMock.Verify(
                r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            Assert.Single(actual);
        }

        [Fact]
        public async Task GetSubscriptions_CacheNotPopulated_ServiceCalled()
        {
            // Arrange
            var repositoryMock = new Mock<ISubscriptionRepository>();
            repositoryMock.Setup(r => r.GetSubscriptions(
              It.IsAny<string>(),
              It.IsAny<string>(),
              It.IsAny<string>(),
              It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<Subscription>() { GetSubscription() });

            (IMemoryCache cache, SubscriptionRepositoryCachingDecorator sut) = GetCacheAndDecorator(repositoryMock);

            string expectedCacheKey = "subscription:re:resourcesu:testsubjectty:event.automated";

            // Act 
            var actual = await sut.GetSubscriptions(
                "resource",
                "testsubject",
                "event.automated",
                CancellationToken.None);

            // Assert
            repositoryMock.Verify(
                r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Single(actual);

            Assert.True(cache.TryGetValue(expectedCacheKey, out var _));
        }

        private static (IMemoryCache Cache, SubscriptionRepositoryCachingDecorator Decorator) GetCacheAndDecorator(Mock<ISubscriptionRepository> repositoryMock)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            var cache = serviceProvider.GetService<IMemoryCache>();

            var decorator = new SubscriptionRepositoryCachingDecorator(
                repositoryMock.Object,
                Options.Create(new PlatformSettings { SubscriptionCachingLifetimeInSeconds = 3600 }),
                cache);

            return (cache, decorator);
        }

        private static Subscription GetSubscription()
        {
            return new Subscription()
            {
                Consumer = "/org/ttd"
            };
        }
    }
}
