using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Decorates <see cref="Interfaces.IRegisterService"/> by caching responses from the Register API.
/// Only <see cref="GetMainUnit"/> responses are cached; other methods are passed through directly.
/// </summary>
public class RegisterServiceCachingDecorator : IRegisterService
{
    private readonly IRegisterService _decoratedService;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterServiceCachingDecorator"/> class.
    /// </summary>
    public RegisterServiceCachingDecorator(
        IRegisterService decoratedService,
        IOptions<PlatformSettings> platformSettings,
        IMemoryCache memoryCache)
    {
        _decoratedService = decoratedService;
        _memoryCache = memoryCache;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetPriority(CacheItemPriority.Normal)
            .SetAbsoluteExpiration(
                TimeSpan.FromSeconds(platformSettings.Value.MainUnitCachingLifetimeInSeconds));
    }

    /// <inheritdoc/>
    public Task<int> PartyLookup(string orgNo, string person)
        => _decoratedService.PartyLookup(orgNo, person);

    /// <inheritdoc/>
    public Task<IEnumerable<PartyIdentifiers>> PartyLookup(
        IEnumerable<string> partyUrnList, CancellationToken cancellationToken)
        => _decoratedService.PartyLookup(partyUrnList, cancellationToken);

    /// <inheritdoc/>
    public async Task<OrganizationRecord> GetMainUnit(
        string organizationUrnValue, CancellationToken cancellationToken)
    {
        string cacheKey = "mainunit:" + organizationUrnValue;

        if (!_memoryCache.TryGetValue(cacheKey, out OrganizationRecord mainUnit))
        {
            mainUnit = await _decoratedService.GetMainUnit(organizationUrnValue, cancellationToken);
            _memoryCache.Set(cacheKey, mainUnit, _cacheOptions);
        }

        return mainUnit;
    }
}
