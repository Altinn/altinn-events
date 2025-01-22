using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Utils;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

/// <summary>
/// A collection of tests related to <see cref="SubscriptionService"/>.
/// </summary>
public class SubscriptionServiceTest
{
    private readonly Mock<ISubscriptionRepository> _repositoryMock = new();

    [Fact]
    public async Task GetAllSubscriptions_SendsIncludeInvalidTrueToRepository()
    {
        // Arrange
        Mock<IClaimsPrincipalProvider> userProvider = new Mock<IClaimsPrincipalProvider>();

        // The organisation number is not used by the logic when an org claim exists.
        userProvider.Setup(u => u.GetUser()).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "na"));
        _repositoryMock.Setup(rm => rm.GetSubscriptionsByConsumer(It.IsAny<string>(), true))
            .ReturnsAsync(new List<Subscription>());

        SubscriptionService subscriptionService = GetSubscriptionService(
            repository: _repositoryMock.Object,
            claimsPrincipalProvider: userProvider.Object);

        // Act
        await subscriptionService.GetAllSubscriptions();

        // Assert
        _repositoryMock.VerifyAll();
    }

    [Fact]
    public async Task CreateSubscription_Unauthorized_ReturnsError()
    {
        // Arrange 
        string expectedErrorMessage = "Not authorized to create a subscription for resource urn:altinn:resource:some-service.";

        var input = new Subscription
        {
            ResourceFilter = "urn:altinn:resource:some-service",
            EndPoint = new Uri("https://automated.com"),
        };

        var sut = GetSubscriptionService(authorizationDecision: false);

        // Act
        (var _, ServiceError actual) = await sut.CompleteSubscriptionCreation(input);

        // Assert
        Assert.Equal(401, actual.ErrorCode);
        Assert.Equal(expectedErrorMessage, actual.ErrorMessage);
    }

    [Fact]
    public async Task CompleteSubscriptionCreation_SubscriptionAlreadyExists_ReturnExisting()
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

        SubscriptionService subscriptionService =
            GetSubscriptionService(_repositoryMock.Object, claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

        // Act
        var result = await subscriptionService.CompleteSubscriptionCreation(subscription);

        // Assert
        _repositoryMock.VerifyAll();
    }

    [Fact]
    public async Task CompleteSubscriptionCreation_SubscriptionNotFound_ReturnNew()
    {
        // Arrange
        int subscriptionId = 645187;
        Subscription subscription = new()
        {
            Id = subscriptionId,
            SourceFilter = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
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

        Mock<IEventsQueueClient> queueMock = new();

        queueMock
            .Setup(q => q.EnqueueSubscriptionValidation(It.Is<string>(s => CheckSubscriptionId(s, 645187))));

        // Act
        SubscriptionService subscriptionService =
              GetSubscriptionService(_repositoryMock.Object, queueMock: queueMock.Object, claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

        var result = await subscriptionService.CompleteSubscriptionCreation(subscription);

        // Assert
        _repositoryMock.VerifyAll();
        queueMock.VerifyAll();
    }

    [Theory]
    [InlineData(EntityType.Org, "ttd", "/org/ttd")]
    [InlineData(EntityType.Organisation, "987654321", "/organisation/987654321")]
    [InlineData(EntityType.User, "1406840", "/user/1406840")]
    public void GetEntityFromPrincipal(EntityType entityType, string entityKeyValue, string expectedEntity)
    {
        // Arrange
        ClaimsPrincipal principal = null;

        switch (entityType)
        {
            case EntityType.User:
                principal = PrincipalUtil.GetClaimsPrincipal(int.Parse(entityKeyValue), 2);
                break;
            case EntityType.Org:
                principal = PrincipalUtil.GetClaimsPrincipal(entityKeyValue, "87364765");
                break;
            case EntityType.Organisation:
                principal = PrincipalUtil.GetClaimsPrincipal(entityKeyValue);
                break;
        }

        Mock<IClaimsPrincipalProvider> claimsPrincipalProviderMock = new();
        claimsPrincipalProviderMock.Setup(
            s => s.GetUser()).Returns(principal);

        SubscriptionService subscriptionService =
            GetSubscriptionService(claimsPrincipalProvider: claimsPrincipalProviderMock.Object);

        // Act
        string actualEntity = subscriptionService.GetEntityFromPrincipal();

        // Assert
        Assert.Equal(expectedEntity, actualEntity);
    }

    public enum EntityType
    {
        User,
        Org,
        Organisation
    }

    private static bool CheckSubscriptionId(string serializedSubscription, int expectedId)
    {
        var subscription = JsonSerializer.Deserialize<Subscription>(serializedSubscription);
        return expectedId == subscription.Id;
    }

    private static SubscriptionService GetSubscriptionService(
        ISubscriptionRepository repository = null,
        bool authorizationDecision = true,
        IEventsQueueClient queueMock = null,
        IClaimsPrincipalProvider claimsPrincipalProvider = null)
    {
        var authoriationMock = new Mock<IAuthorization>();
        authoriationMock
            .Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
            .ReturnsAsync(authorizationDecision);

        return new SubscriptionService(
            repository ?? new SubscriptionRepositoryMock(),
            authoriationMock.Object,
            queueMock ?? new Mock<IEventsQueueClient>().Object,
            claimsPrincipalProvider ?? new Mock<IClaimsPrincipalProvider>().Object);
    }
}
