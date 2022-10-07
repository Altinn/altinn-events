using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
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
    public class PushOutboundService : IPushOutboundService
    {
        private readonly IEventsQueueClient _queue;

        private readonly ISubscriptionService _subscriptionService;
        private readonly IAuthorization _authorizationService;
        private readonly PlatformSettings _platformSettings;

        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _orgSubscriptioncacheEntryOptions;
        private readonly MemoryCacheEntryOptions _partySubscriptioncacheEntryOptions;
        private readonly MemoryCacheEntryOptions _orgAuthorizationEntryOptions;

        private readonly ILogger<IPushOutboundService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushOutboundService"/> class.
        /// </summary>
        public PushOutboundService(
            IEventsQueueClient queueService,
            ISubscriptionService subscriptionService,
            IAuthorization authorizationService,
            IOptions<PlatformSettings> platformSettings,
            IMemoryCache memoryCache,
            ILogger<IPushOutboundService> logger)
        {
            _queue = queueService;
            _subscriptionService = subscriptionService;
            _authorizationService = authorizationService;
            _platformSettings = platformSettings.Value;
            _memoryCache = memoryCache;
            _logger = logger;

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
        public async Task PushOutbound(CloudEvent cloudEvent)
        {
            string sourceFilter = GetSourceFilter(cloudEvent.Source);

            if (!string.IsNullOrEmpty(sourceFilter))
            {
                List<Subscription> orgSubscriptions = await GetOrgSubscriptions(sourceFilter, cloudEvent.Subject, cloudEvent.Type);
                await AuthorizeAndPush(cloudEvent, orgSubscriptions);

                List<Subscription> subscriptions = await GetSubscriptionExcludeOrgs(sourceFilter, cloudEvent.Subject, cloudEvent.Type);
                await AuthorizeAndPush(cloudEvent, subscriptions);
            }
        }

        private async Task PushToOutboundQueue(CloudEventEnvelope cloudEventEnvelope)
        {
            PushQueueReceipt receipt = await _queue.PushToOutboundQueue(JsonSerializer.Serialize(cloudEventEnvelope));
            string cloudEventId = cloudEventEnvelope.CloudEvent.Id;
            int subscriptionId = cloudEventEnvelope.SubscriptionId;

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // PushToOutboundQueue // Failed to push event envelope {EventId} to consumer with subscriptionId {subscriptionId}.", cloudEventId, subscriptionId);
            }
        }

        private string GetSourceFilter(Uri source)
        {
            if (source.DnsSafeHost.Contains(_platformSettings.AppsDomain))
            {
                return source.OriginalString.Substring(0, source.OriginalString.IndexOf(source.Segments[3]) - 1);
            }
            else
            {
                return string.Empty;
            }
        }

        private async Task AuthorizeAndPush(CloudEvent cloudEvent, List<Subscription> subscriptions)
        {
            foreach (Subscription subscription in subscriptions)
            {
                await AuthorizeAndPush(cloudEvent, subscription);
            }
        }

        private async Task AuthorizeAndPush(CloudEvent cloudEvent, Subscription subscription)
        {
            if (await AuthorizeConsumerForAltinnAppEvent(cloudEvent, subscription.Consumer))
            {
                CloudEventEnvelope cloudEventEnvelope = MapToEnvelope(cloudEvent, subscription);
                await PushToOutboundQueue(cloudEventEnvelope);
            }
        }

        private async Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer)
        {
            string cacheKey = GetAltinnAppAuthorizationCacheKey(GetSourceFilter(cloudEvent.Source), consumer);

            bool isAuthorized;

            if (!_memoryCache.TryGetValue(cacheKey, out isAuthorized))
            {
                isAuthorized = await _authorizationService.AuthorizeConsumerForAltinnAppEvent(cloudEvent, consumer);
                _memoryCache.Set(cacheKey, isAuthorized, _orgAuthorizationEntryOptions);
            }

            return isAuthorized;
        }

        private async Task<List<Subscription>> GetOrgSubscriptions(string sourceFilter, string subject, string type)
        {
            string cacheKey = GetOrgAppSubscriptionCacheKey(sourceFilter, subject, type);
            List<Subscription> orgSubscriptions;

            if (!_memoryCache.TryGetValue(cacheKey, out orgSubscriptions))
            {
                orgSubscriptions = await _subscriptionService.GetOrgSubscriptions(
                sourceFilter,
                subject,
                type);

                _memoryCache.Set(cacheKey, orgSubscriptions, _orgSubscriptioncacheEntryOptions);
            }

            return orgSubscriptions;
        }

        private async Task<List<Subscription>> GetSubscriptionExcludeOrgs(string sourceFilter, string subject, string type)
        {
            string cacheKey = GetPartyAppSubscriptionCacheKey(sourceFilter, subject, type);
            List<Subscription> orgSubscriptions;

            if (!_memoryCache.TryGetValue(cacheKey, out orgSubscriptions))
            {
                orgSubscriptions = await _subscriptionService.GetSubscriptions(
                sourceFilter,
                subject,
                type);
                _memoryCache.Set(cacheKey, orgSubscriptions, _partySubscriptioncacheEntryOptions);
            }

            return orgSubscriptions;
        }

        private static CloudEventEnvelope MapToEnvelope(CloudEvent cloudEvent, Subscription subscription)
        {
            CloudEventEnvelope cloudEventEnvelope = new CloudEventEnvelope()
            {
                CloudEvent = cloudEvent,
                Consumer = subscription.Consumer,
                Pushed = DateTime.Now,
                SubscriptionId = subscription.Id,
                Endpoint = subscription.EndPoint
            };

            return cloudEventEnvelope;
        }

        private static string GetOrgAppSubscriptionCacheKey(string source, string subject, string type)
        {
            if (source == null)
            {
                return null;
            }

            return "orgsubscription:so:" + source + "su:" + subject + "ty:" + type;
        }

        private static string GetPartyAppSubscriptionCacheKey(string source, string subject, string type)
        {
            if (source == null)
            {
                return null;
            }

            return "partysubscription:so:" + source + "su:" + subject + "ty:" + type;
        }

        private static string GetAltinnAppAuthorizationCacheKey(string sourceFilter, string consumer)
        {
            if (sourceFilter == null)
            {
                return null;
            }

            return "authorizationdecision:so:" + sourceFilter + "co:" + consumer;
        }
    }
}
