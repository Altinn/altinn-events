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
        ///   Push an event. Three subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to three different subscribers
        /// Success criteria:
        ///   The event is pushed to three subscribers
        /// </summary>
        [Fact]
        public async void Push_TwoMatchingAndValidSubscriptions_AddedToqueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            QueueServiceMock queueServiceMock = new();

            var service = GetPushEventService(queueServiceMock);

            // Act
            await service.Push(cloudEvent);

            // Assert
            Assert.True(queueServiceMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Equal(3, queueServiceMock.OutboundQueue[cloudEvent.Id].Count);
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

            QueueServiceMock queueServiceMock = new();
            var service = GetPushEventService(queueServiceMock);

            // Act
            await service.Push(cloudEvent);

            // Assert
            Assert.True(queueServiceMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Single(queueServiceMock.OutboundQueue[cloudEvent.Id]);
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
        public async void Push_QueueReportsFailue_ErrorIsLogged()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1");

            var queueMock = new Mock<IQueueService>();
            queueMock.Setup(q => q.PushToOutboundQueue(It.IsAny<string>()))
                    .ReturnsAsync(new PushQueueReceipt { Success = false });

            var loggerMock = new Mock<ILogger<IPushEvent>>();

            var service = GetPushEventService(queueMock: queueMock.Object, loggerMock: loggerMock.Object);

            // Act
            await service.Push(cloudEvent);

            // Assert
            loggerMock.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("// EventsService // StoreCloudEvent // Failed to push event envelope", StringComparison.InvariantCultureIgnoreCase)),
                   It.IsAny<Exception>(),
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
            queueMock.VerifyAll();
        }

        private static IPushEvent GetPushEventService(IQueueService queueMock, ILogger<IPushEvent> loggerMock = null)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            if (loggerMock == null)
            {
                loggerMock = new Mock<ILogger<IPushEvent>>().Object;
            }

            IAuthorization authorizationMock = new AuthorizationService(new PepWithPDPAuthorizationMockSI());
            var service = new PushEventService(
                queueMock,
                new SubscriptionRepositoryMock(),
                authorizationMock,
                Options.Create(new PlatformSettings
                {
                    SubscriptionCachingLifetimeInSeconds = 60,
                    AppsDomain = "apps.altinn.no"
                }),
                memoryCache,
                loggerMock);

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
