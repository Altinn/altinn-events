#nullable enable

using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

public class RegisterServiceCachingDecoratorTest
{
    [Fact]
    public async Task GetMainUnit_FirstCall_DelegatesToInnerService()
    {
        // Arrange
        var expected = new OrganizationRecord { OrganizationIdentifier = "311443755", PartyId = 51326197 };
        const string urn = "urn:altinn:party:id:51326197";

        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = GetDecorator(innerMock.Object);

        // Act
        var result = await sut.GetMainUnit(urn, CancellationToken.None);

        // Assert
        Assert.Equal(expected.OrganizationIdentifier, result?.OrganizationIdentifier);
        innerMock.Verify(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMainUnit_SecondCallSameUrn_ReturnsCachedValueWithoutCallingInner()
    {
        // Arrange
        var expected = new OrganizationRecord { OrganizationIdentifier = "311443755", PartyId = 51326197 };
        const string urn = "urn:altinn:party:id:51326197";

        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = GetDecorator(innerMock.Object);

        // Act
        await sut.GetMainUnit(urn, CancellationToken.None);
        var result = await sut.GetMainUnit(urn, CancellationToken.None);

        // Assert
        Assert.Equal(expected.OrganizationIdentifier, result?.OrganizationIdentifier);
        innerMock.Verify(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMainUnit_DifferentUrns_CallsInnerForEach()
    {
        // Arrange
        const string urn1 = "urn:altinn:party:id:51326197";
        const string urn2 = "urn:altinn:party:id:51326198";

        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.GetMainUnit(urn1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRecord { OrganizationIdentifier = "311443755" });
        innerMock.Setup(s => s.GetMainUnit(urn2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRecord { OrganizationIdentifier = "314249879" });

        var sut = GetDecorator(innerMock.Object);

        // Act
        await sut.GetMainUnit(urn1, CancellationToken.None);
        await sut.GetMainUnit(urn2, CancellationToken.None);

        // Assert — each unique URN hits the inner service exactly once
        innerMock.Verify(s => s.GetMainUnit(urn1, It.IsAny<CancellationToken>()), Times.Once);
        innerMock.Verify(s => s.GetMainUnit(urn2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMainUnit_NullResponse_IsCachedAndInnerCalledOnce()
    {
        // Arrange — null means no main unit found; we still want to cache it
        const string urn = "urn:altinn:organization:identifier-no:000000000";

        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationRecord?)null);

        var sut = GetDecorator(innerMock.Object);

        // Act
        var first = await sut.GetMainUnit(urn, CancellationToken.None);
        var second = await sut.GetMainUnit(urn, CancellationToken.None);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        innerMock.Verify(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMainUnit_AfterCacheExpiry_CallsInnerAgain()
    {
        // Arrange — use a very short TTL so we can expire the entry manually
        const string urn = "urn:altinn:party:id:51326197";
        var expected = new OrganizationRecord { OrganizationIdentifier = "311443755" };

        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = GetDecorator(innerMock.Object, cache, lifetimeSeconds: 1);

        // Act — first call populates the cache
        await sut.GetMainUnit(urn, CancellationToken.None);

        // Evict the entry manually to simulate expiry
        cache.Remove("mainunit:" + urn);

        // Second call should hit the inner service again
        await sut.GetMainUnit(urn, CancellationToken.None);

        // Assert
        innerMock.Verify(s => s.GetMainUnit(urn, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PartyLookup_SingleOrg_DelegatesToInner()
    {
        // Arrange
        var innerMock = new Mock<IRegisterService>();
        innerMock.Setup(s => s.PartyLookup("311443755", null))
            .ReturnsAsync(51326197);

        var sut = GetDecorator(innerMock.Object);

        // Act
        var result = await sut.PartyLookup("311443755", null);

        // Assert
        Assert.Equal(51326197, result);
        innerMock.Verify(s => s.PartyLookup("311443755", null), Times.Once);
    }

    private static RegisterServiceCachingDecorator GetDecorator(
        IRegisterService inner,
        IMemoryCache? cache = null,
        int lifetimeSeconds = 3600)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());

        var settings = Options.Create(new PlatformSettings
        {
            MainUnitCachingLifetimeInSeconds = lifetimeSeconds
        });

        return new RegisterServiceCachingDecorator(inner, settings, cache);
    }
}
