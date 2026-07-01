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
/// Wraps <see cref="IRegisterApiClient"/> with caching for Register API responses.
/// Only <see cref="GetMainUnit"/> responses are cached; other methods are passed through directly.
/// </summary>
public class CachingRegisterService : IRegisterService
{
    private readonly IRegisterApiClient _inner;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingRegisterService"/> class.
    /// </summary>
    public CachingRegisterService(
        IRegisterApiClient inner,
        IOptions<PlatformSettings> platformSettings,
        IMemoryCache memoryCache)
    {
        _inner = inner;
        _memoryCache = memoryCache;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetPriority(CacheItemPriority.Normal)
            .SetAbsoluteExpiration(
                TimeSpan.FromSeconds(platformSettings.Value.MainUnitCachingLifetimeInSeconds));
    }

    /// <inheritdoc/>
    public Task<int> PartyLookup(string orgNo, string person)
        => _inner.PartyLookup(orgNo, person);

    /// <inheritdoc/>
    public Task<IEnumerable<PartyIdentifiers>> PartyLookup(
        IEnumerable<string> partyUrnList, CancellationToken cancellationToken)
        => _inner.PartyLookup(partyUrnList, cancellationToken);

    /// <inheritdoc/>
    public async Task<OrganizationRecord> GetMainUnit(
        string organizationUrnValue, CancellationToken cancellationToken)
    {
        string cacheKey = "mainunit:" + organizationUrnValue;

        if (!_memoryCache.TryGetValue(cacheKey, out OrganizationRecord mainUnit))
        {
            mainUnit = await _inner.GetMainUnit(organizationUrnValue, cancellationToken);
            _memoryCache.Set(cacheKey, mainUnit, _cacheOptions);
        }

        return mainUnit;
    }
}
