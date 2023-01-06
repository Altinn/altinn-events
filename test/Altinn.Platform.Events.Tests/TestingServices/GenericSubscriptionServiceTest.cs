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
    public class GenericSubscriptionServiceTest
    {
        private readonly Mock<ISubscriptionRepository> _repositoryMock = new();

        [Fact]
        public async Task CreateSubscription_OrgCredentials_CreatedBySetCorrectly()
        {
        }

        [Fact]
        public async Task CreateSubscription_ConsumerNotProvided_ReturnsError()
        {
        }

        [Fact]
        public async Task CreateSubscription_ValidAndAuthorizedSubscription_ReturnsNewSubscription()
        {
        }

        private static GenericSubscriptionService GetGenericSubscriptionService(
            ISubscriptionRepository repository = null,
            IClaimsPrincipalProvider claimsPrincipalProvider = null)
        {
            return new GenericSubscriptionService(
                repository ?? new SubscriptionRepositoryMock(),
                new EventsQueueClientMock(),
                claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object,
                new Mock<IRegisterService>().Object);
        }
    }
}
