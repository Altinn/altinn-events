using System;
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
    /// A collection of tests related to <see cref="GenericSubscriptionService"/>.
    /// </summary>
    public class GenericSubscriptionServiceTest
    {
        [Fact]
        public async Task CreateSubscription_ValidAndAuthorizedSubscription_ReturnsNewSubscription()
        {
            // Arrange 
            var input = new Subscription
            {
                Consumer = "/owner/223596",
                SubjectFilter = "/dog/bruno",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                SourceFilter = new Uri("https://doggy-daycare.no/booking")
            };

            Mock<ISubscriptionRepository> repoMock = new();
            var sut = GetGenericSubscriptionService(repoMock);

            // Act
            (var actual, ServiceError _) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal("/org/ttd", actual.CreatedBy);
            repoMock.VerifyAll();
        }

        [Fact]
        public async Task CreateSubscription_AlternaticSubjectFilterProvided_ReturnsError()
        {
            // Arrange 
            string expectedErrorMessage = "AlternativeSubject is not supported for subscriptions on generic event sources.";

            var input = new Subscription
            {
                Consumer = "/owner/223596",
                SubjectFilter = "/dog/bruno",
                EndPoint = new Uri("https://fantastiske-hundepassere.no/events"),
                SourceFilter = new Uri("https://doggy-daycare.no/booking"),
                AlternativeSubjectFilter = "/object/123456"
            };

            var sut = GetGenericSubscriptionService();

            // Act
            (var _, ServiceError actual) = await sut.CreateSubscription(input);

            // Assert
            Assert.Equal(400, actual.ErrorCode);
            Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
        }

        private static GenericSubscriptionService GetGenericSubscriptionService(
            Mock<ISubscriptionRepository> repoMock = null)
        {
            var claimsProviderMock = new Mock<IClaimsPrincipalProvider>();
            claimsProviderMock.Setup(
                s => s.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "1234567892"));

            if (repoMock == null)
            {
                repoMock = new();
            }

            repoMock
                 .Setup(r => r.FindSubscription(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Subscription)null);

            repoMock
                .Setup(r => r.CreateSubscription(It.IsAny<Subscription>(), It.IsAny<string>()))
                .ReturnsAsync((Subscription s, string _) =>
                {
                    s.Id = new Random().Next(1, int.MaxValue);
                    s.Created = DateTime.Now;

                    return s;
                });

            return new GenericSubscriptionService(
                repoMock.Object,
                new Mock<IRegisterService>().Object,
                new EventsQueueClientMock(),
                claimsProviderMock.Object)
            {
            };
        }
    }
}
