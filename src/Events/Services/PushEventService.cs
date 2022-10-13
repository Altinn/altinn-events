using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
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

        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IAuthorization _authorizationService;
        private readonly PlatformSettings _platformSettings;

        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _subscriptionCacheEntryOptions;
        private readonly MemoryCacheEntryOptions _orgAuthorizationEntryOptions;

        private readonly ILogger<IPushEvent> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushEventService"/> class.
        /// </summary>
        public PushEventService(
            IQueueService queueService,
            ISubscriptionRepository repository,
            IAuthorization authorizationService,
            IOptions<PlatformSettings> platformSettings,
            IMemoryCache memoryCache,
            ILogger<IPushEvent> logger)
        {
            _queue = queueService;
            _subscriptionRepository = repository;
            _authorizationService = authorizationService;
            _platformSettings = platformSettings.Value;
            _memoryCache = memoryCache;
            _logger = logger;

            _subscriptionCacheEntryOptions = new MemoryCacheEntryOptions()
                .SetPriority(CacheItemPriority.High)
                .SetAbsoluteExpiration(
                    new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
            _orgAuthorizationEntryOptions = new MemoryCacheEntryOptions()
              .SetPriority(CacheItemPriority.High)
              .SetAbsoluteExpiration(
                  new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
        }

        /// <inheritdoc/>
        public async Task Push(CloudEvent cloudEvent)
        {            
                List<Subscription> subscriptions = await GetSubscriptions(cloudEvent.Source.ToString(), cloudEvent.Subject, cloudEvent.Type);
                await AuthorizeAndPush(cloudEvent, subscriptions);            
        }

        private async Task PushToConsumer(CloudEventEnvelope cloudEventEnvelope)
        {
            PushQueueReceipt receipt = await _queue.PushToOutboundQueue(JsonSerializer.Serialize(cloudEventEnvelope));
            string cloudEventId = cloudEventEnvelope.CloudEvent.Id;
            int subscriptionId = cloudEventEnvelope.SubscriptionId;

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // StoreCloudEvent // Failed to push event envelope {EventId} to comsumer with subscriptionId {subscriptionId}.", cloudEventId, subscriptionId);
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
                await PushToConsumer(cloudEventEnvelope);
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

        private async Task<List<Subscription>> GetSubscriptions(string sourceFilter, string subject, string type)
        {
            sourceFilter = sourceFilter.ToLower();
            subject = subject.ToLower();
            type = type.ToLower();

            string cacheKey = GetSubscriptionCacheKey(sourceFilter, subject, type);

            List<Subscription> subscriptions;

            if (!_memoryCache.TryGetValue(cacheKey, out subscriptions))
            {
                subscriptions = await _subscriptionRepository.GetSubscriptions(
                      sourceFilter,
                      subject,
                      type,
                      CancellationToken.None);

                _memoryCache.Set(cacheKey, subscriptions, _subscriptionCacheEntryOptions);
            }

            return subscriptions;
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

        private static string GetSubscriptionCacheKey(string source, string subject, string type)
        {
            if (source == null)
            {
                return null;
            }

            return "subscription:so:" + source + "su:" + subject + "ty:" + type;
        }

        private static string GetAltinnAppAuthorizationCacheKey(string sourceFilter, string consumer)
        {
            if (sourceFilter == null)
            {
                return null;
            }

            return "authorizationdecision:so:" + sourceFilter + "co:" + consumer;
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
    }
}
