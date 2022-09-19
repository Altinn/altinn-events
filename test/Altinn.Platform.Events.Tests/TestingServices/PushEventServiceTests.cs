using System;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.UnitTest.Mocks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Represents a collection of integration tests of the <see cref="PushEventService"/>.
    /// </summary>
    public class PushEventServiceTests
    {
        /// <summary>
        /// Scenario:
        ///   Push an event. Two subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to two different subscribers
        /// Success criteria:
        ///   The event is pushed to two subscribers
        /// </summary>
        [Fact]
        public async void Push_TwoMatchingAndValidSubscriptions_AddedToqueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            var service = GetPushEventService();

            // Act
            await service.Push(cloudEvent);

            // Assert
            Assert.True(QueueServiceMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Equal(2, QueueServiceMock.OutboundQueue[cloudEvent.Id].Count);
        }

        /// <summary>
        /// Scenario:
        ///   Post an event for outbound push. One subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to two different subscribers
        /// Success criteria:
        ///   The event is pushed to two subscribers
        /// </summary>
        [Fact]
        public async void Push_OneMatchingAndValidSubscriptions_AddedToqueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1");

            var service = GetPushEventService();

            // Act
            await service.Push(cloudEvent);

            // Assert
            Assert.True(QueueServiceMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Single(QueueServiceMock.OutboundQueue[cloudEvent.Id]);
        }

        private IPushEvent GetPushEventService()
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            IAuthorization authorizationMock = new AuthorizationService(new PepWithPDPAuthorizationMockSI());
            var service = new PushEventService(
                new QueueServiceMock(),
                new SubscriptionService(new SubscriptionRepositoryMock(), new QueueServiceMock()),
                authorizationMock,
                Options.Create(new PlatformSettings
                {
                    SubscriptionCachingLifetimeInSeconds = 60,
                    AppsDomain = "apps.altinn.no"
                }),
                memoryCache,
                new Mock<ILogger<IPushEvent>>().Object);

            return service;
        }

        private static CloudEvent GetCloudEvent(Uri source, string subject, string type)
        {
            CloudEvent cloudEvent = new()
            {
                Id = Guid.NewGuid().ToString(),
                SpecVersion = "1.0",
                Type = type,
                Source = source,
                Time = DateTime.Now,
                Subject = subject,
                Data = "something/extra",
            };

            return cloudEvent;
        }
    }
}
