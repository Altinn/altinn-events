using System;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// An implementation of the push service
    /// </summary>
    public class PushEventService : IPushEvent
    {
        private readonly IQueueService _queue;

        private readonly ISubscriptionService _subscriptionService;
        private readonly IAuthorization _authorizationService;
        private readonly PlatformSettings _platformSettings;

        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _orgSubscriptioncacheEntryOptions;
        private readonly MemoryCacheEntryOptions _partySubscriptioncacheEntryOptions;
        private readonly MemoryCacheEntryOptions _orgAuthorizationEntryOptions;

        private readonly ILogger<IPushEvent> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushEventService"/> class.
        /// </summary>
        public PushEventService(
            IQueueService queueService,
            ISubscriptionService subscriptionService,
            IAuthorization authorizationService,
            IOptions<PlatformSettings> platformSettings,
            IMemoryCache memoryCache)
        {
            _queue = queueService;
            _subscriptionService = subscriptionService;
            _authorizationService = authorizationService;
            _platformSettings = platformSettings.Value;
            _memoryCache = memoryCache;
            _orgSubscriptioncacheEntryOptions = new MemoryCacheEntryOptions()
                .SetPriority(CacheItemPriority.High)
                .SetAbsoluteExpiration(
                    new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
            _partySubscriptioncacheEntryOptions = new MemoryCacheEntryOptions()
              .SetPriority(CacheItemPriority.Normal)
              .SetAbsoluteExpiration(
                  new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
            _orgAuthorizationEntryOptions = new MemoryCacheEntryOptions()
              .SetPriority(CacheItemPriority.High)
              .SetAbsoluteExpiration(
                  new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
        }

        /// <inheritdoc/>
        public Task Push(CloudEvent cloudEvent, Subscription subscription)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task PushToConsumer(CloudEventEnvelope cloudEventEnvelope)
        {
            PushQueueReceipt receipt = await _queue.PushToOutboundQueue(JsonSerializer.Serialize(cloudEventEnvelope));
            string cloudEventId = cloudEventEnvelope.CloudEvent.Id;
            int subscriptionId = cloudEventEnvelope.SubscriptionId;

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // StoreCloudEvent // Failed to push event envelope {EventId} to comsumer with subscriptionId {subscriptionId}.", cloudEventId, subscriptionId);
            }
        }
    }
}
