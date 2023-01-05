using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>.
    /// Decorates an implementation of ISubscriptionRepository by caching the subscription(s).
    /// If available, object is retrieved from cache without calling the service
    /// </summary>
    public class SubscriptionRepositoryCachingDecorator : ISubscriptionRepository
    {
        private readonly ISubscriptionRepository _decoratedService;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRepositoryCachingDecorator"/> class.
        /// </summary>
        public SubscriptionRepositoryCachingDecorator(
            ISubscriptionRepository decoratedService,
            IOptions<PlatformSettings> platformSettings,
            IMemoryCache memoryCache)
        {
            _decoratedService = decoratedService;
            _memoryCache = memoryCache;
            _cacheOptions = new MemoryCacheEntryOptions()
               .SetPriority(CacheItemPriority.High)
               .SetAbsoluteExpiration(
                   new TimeSpan(0, 0, platformSettings.Value.SubscriptionCachingLifetimeInSeconds));
        }

        /// <inheritdoc/>
        public async Task<List<Subscription>> GetSubscriptions(List<string> sourceFilterHashes, string source, string subject, string type, CancellationToken ct)
        {
            string cacheKey = GetSubscriptionCacheKey(source.ToString(), subject, type);

            if (!_memoryCache.TryGetValue(cacheKey, out List<Subscription> subscriptions))
            {
                subscriptions = await _decoratedService.GetSubscriptions(sourceFilterHashes, source, subject, type, ct);

                _memoryCache.Set(cacheKey, subscriptions, _cacheOptions);
            }

            return subscriptions;
        }

        /// <inheritdoc/>
        public async Task<Subscription> CreateSubscription(Subscription eventsSubscription, string sourceFilterHash)
        {
            return await _decoratedService.CreateSubscription(eventsSubscription, sourceFilterHash);
        }

        /// <inheritdoc/>
        public async Task DeleteSubscription(int id)
        {
            await _decoratedService.DeleteSubscription(id);
        }

        /// <inheritdoc/>
        public async Task<Subscription> FindSubscription(Subscription eventsSubscription, CancellationToken ct)
        {
            return await _decoratedService.FindSubscription(eventsSubscription, ct);
        }

        /// <inheritdoc/>
        public async Task<Subscription> GetSubscription(int id)
        {
            return await _decoratedService.GetSubscription(id);
        }
      
        /// <inheritdoc/>
        public async Task<List<Subscription>> GetSubscriptionsByConsumer(string consumer, bool includeInvalid)
        {
            return await _decoratedService.GetSubscriptionsByConsumer(consumer, includeInvalid);
        }

        /// <inheritdoc/>
        public async Task SetValidSubscription(int id)
        {
            await _decoratedService.SetValidSubscription(id);
        }

        private static string GetSubscriptionCacheKey(string source, string subject, string type)
        {
            if (source == null)
            {
                return null;
            }

            return "subscription:so:" + source + "su:" + subject + "ty:" + type;
        }
    }
}
