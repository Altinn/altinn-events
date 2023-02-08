using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// An implementation of the push service
    /// </summary>
    public class OutboundService : IOutboundService
    {
        private readonly IEventsQueueClient _queueClient;

        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IAuthorization _authorizationService;
        private readonly PlatformSettings _platformSettings;

        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _orgAuthorizationEntryOptions;

        private readonly ILogger<IOutboundService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundService"/> class.
        /// </summary>
        public OutboundService(
            IEventsQueueClient queueClient,
            ISubscriptionRepository repository,
            IAuthorization authorizationService,
            IOptions<PlatformSettings> platformSettings,
            IMemoryCache memoryCache,
            ILogger<IOutboundService> logger)
        {
            _queueClient = queueClient;
            _subscriptionRepository = repository;
            _authorizationService = authorizationService;
            _platformSettings = platformSettings.Value;
            _memoryCache = memoryCache;
            _logger = logger;

            _orgAuthorizationEntryOptions = new MemoryCacheEntryOptions()
              .SetPriority(CacheItemPriority.High)
              .SetAbsoluteExpiration(
                  new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
        }

        /// <inheritdoc/>
        public async Task PostOutbound(CloudEvent cloudEvent)
        {
            Uri eventSource = cloudEvent.Source;

            if (IsAppEvent(cloudEvent))
            {
                eventSource = GetSourceFilter(cloudEvent.Source);
            }
           
            List<Subscription> subscriptions = await _subscriptionRepository.GetSubscriptions(
                 eventSource.GetMD5HashSets(),
                 eventSource.ToString(),
                 cloudEvent.Subject,
                 cloudEvent.Type,
                 CancellationToken.None);

            await AuthorizeAndPush(cloudEvent, subscriptions);
        }

        private async Task AuthorizeAndPush(CloudEvent cloudEvent, List<Subscription> subscriptions)
        {
            foreach (Subscription subscription in subscriptions)
            {
                await AuthorizeAndEnqueueOutbound(cloudEvent, subscription);
            }
        }

        private async Task AuthorizeAndEnqueueOutbound(CloudEvent cloudEvent, Subscription subscription)
        {
            var authorized = 
                IsAppEvent(cloudEvent)
                ? await AuthorizeConsumerForAltinnAppEvent(cloudEvent, subscription.Consumer)
                : await AuthorizeConsumerForGenericEvent(cloudEvent, subscription.Consumer);

            if (authorized) 
            { 
                CloudEventEnvelope cloudEventEnvelope = MapToEnvelope(cloudEvent, subscription);

                var receipt = await _queueClient.EnqueueOutbound(cloudEventEnvelope.Serialize());

                if (!receipt.Success)
                {
                    _logger.LogError(receipt.Exception, "// OutboundService // EnqueueOutbound // Failed to send event envelope {EventId} to consumer with subscriptionId {subscriptionId}.", cloudEvent.Id, subscription.Id);
                }
            }
        }

        private async Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer)
        {
            string cacheKey = GetAltinnAppAuthorizationCacheKey(GetSourceFilter(cloudEvent.Source).ToString(), consumer);

            if (!_memoryCache.TryGetValue(cacheKey, out bool isAuthorized))
            {
                isAuthorized = await _authorizationService.AuthorizeConsumerForAltinnAppEvent(cloudEvent, consumer);
                _memoryCache.Set(cacheKey, isAuthorized, _orgAuthorizationEntryOptions);
            }

            return isAuthorized;
        }

        private async Task<bool> AuthorizeConsumerForGenericEvent(CloudEvent cloudEvent, string consumer)
        {
            return await Task.FromResult(true);
        }

        private static CloudEventEnvelope MapToEnvelope(CloudEvent cloudEvent, Subscription subscription)
        {
            CloudEventEnvelope cloudEventEnvelope = new()
            {
                CloudEvent = cloudEvent,
                Consumer = subscription.Consumer,
                Pushed = DateTime.Now,
                SubscriptionId = subscription.Id,
                Endpoint = subscription.EndPoint
            };

            return cloudEventEnvelope;
        }

        private static string GetAltinnAppAuthorizationCacheKey(string sourceFilter, string consumer)
        {
            if (sourceFilter == null)
            {
                return null;
            }

            return "authorizationdecision:so:" + sourceFilter + "co:" + consumer;
        }

        /// <summary>
        /// Simplifies the source uri of an Altinn App to only include host and app/org part of path
        /// </summary>
        private Uri GetSourceFilter(Uri source)
        {
            if (source.DnsSafeHost.Contains(_platformSettings.AppsDomain))
            {
                // including schema in uri
                return new Uri(source.AbsoluteUri.Substring(0, source.AbsoluteUri.IndexOf(source.Segments[3]) - 1));
            }
            else
            {
                return source;
            }
        }

        private bool IsAppEvent(CloudEvent cloudEvent)
        {
            return !string.IsNullOrEmpty(cloudEvent.Source.Host) && cloudEvent.Source.Host.EndsWith(_platformSettings.AppsDomain);
        }
    }
}