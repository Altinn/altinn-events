using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
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
    /// A collection of tests related to <see cref="GenericSubscriptionService"/>.
    /// </summary>
    public class GenericSubscriptionServiceTest
    {
        [Theory]
        [InlineData("https://fantastiske-hundepassere.no/events", "/dog/bruno")]
        [InlineData("https://fantastiske-hundepassere.no", "/dog/bruno")]
        public async Task CreateSubscription_ValidAndAuthorizedSubscription_ReturnsNewSubscription(string endpoint, string subjectFilter)
        {
            // Arrange 
            var input = new Subscription
            {
                ResourceFilter = "urn:altinn:resource:some-service",
                SubjectFilter = subjectFilter,
                EndPoint = new Uri(endpoint),
            };

            Mock<ISubscriptionRepository> repoMock = new();
            var sut = GetGenericSubscriptionService(repoMock);

            // Act
            (var actual, ServiceError _) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal("/org/ttd", actual.CreatedBy);
            Assert.Equal("/org/ttd", actual.Consumer);
            repoMock.VerifyAll();
        }

        [Fact]
        public async Task CreateSubscription_AlternaticSubjectFilterProvided_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "AlternativeSubject filter is not supported for subscriptions on this resource.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                AlternativeSubjectFilter = "/object/123456"
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task CreateSubscription_InvalidUrnProvidedAsSourceFilter_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "Source filter is not supported for subscriptions on this resource.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                ResourceFilter = "urn:altinn:resource:some-service",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                SourceFilter = new Uri("telnet://ole:qwerty@altinn.no:45432/")
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        [Fact]
        public async Task CreateSubscription_MissingResourceFilter_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "Resource filter is required.";

            var input = new Subscription
            {
                SubjectFilter = "/dog/bruno",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        private static GenericSubscriptionService GetGenericSubscriptionService(
            Mock<ISubscriptionRepository> repoMock = null,
            IMessageBus messageBus = null,
            bool isAuthorized = true)
        {
            var claimsProviderMock = new Mock<IClaimsPrincipalProvider>();
            claimsProviderMock.Setup(
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "1234567892"));

            var authorizationMock = new Mock<IAuthorization>();
            authorizationMock.Setup(
                a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
                .ReturnsAsync(isAuthorized);

            repoMock ??= new();

            repoMock
                 .Setup(r => r.FindSubscription(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Subscription)null);

            repoMock
                .Setup(r => r.CreateSubscription(It.IsAny<Subscription>()))
                        .ReturnsAsync((Subscription s) =>
                        {
                            s.Id = new Random().Next(1, int.MaxValue);
                            s.Created = DateTime.Now;

                            return s;
                        });

            // Create mock for IMessageBus if not provided
            if (messageBus == null)
            {
                var messageBusMock = new Mock<IMessageBus>();
                messageBusMock
                    .Setup(m => m.PublishAsync(It.IsAny<object>(), It.IsAny<DeliveryOptions>()))
                    .Returns(ValueTask.CompletedTask);
                messageBus = messageBusMock.Object;
            }

            return new GenericSubscriptionService(
                repoMock.Object,
                authorizationMock.Object,
                messageBus,
                claimsProviderMock.Object,
                Options.Create(new PlatformSettings()),
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<GenericSubscriptionService>>().Object);
        }
    }
}
