using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Telemetry;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// An implementation of the push service
/// </summary>
public class OutboundService : IOutboundService
{
    private readonly IEventsQueueClient _queueClient;
    private readonly IMessageBus _bus;
    private readonly ITraceLogService _traceLogService;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IAuthorization _authorizationService;
    private readonly PlatformSettings _platformSettings;

    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _consumerAuthorizationEntryOptions;

    private readonly ILogger<OutboundService> _logger;
    private readonly TelemetryClient _telemetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundService"/> class.
    /// </summary>
    public OutboundService(
        IEventsQueueClient queueClient,
        IMessageBus bus,
        ITraceLogService traceLogService,
        ISubscriptionRepository repository,
        IAuthorization authorizationService,
        IOptions<PlatformSettings> platformSettings,
        IMemoryCache memoryCache,
        ILogger<OutboundService> logger,
        TelemetryClient telemetry)
    {
        _queueClient = queueClient;
        _bus = bus;
        _traceLogService = traceLogService;
        _subscriptionRepository = repository;
        _authorizationService = authorizationService;
        _platformSettings = platformSettings.Value;
        _memoryCache = memoryCache;
        _logger = logger;
        _telemetry = telemetry;

        _consumerAuthorizationEntryOptions = new MemoryCacheEntryOptions()
          .SetPriority(CacheItemPriority.High)
          .SetAbsoluteExpiration(
              new TimeSpan(0, 0, _platformSettings.SubscriptionCachingLifetimeInSeconds));
    }

    /// <inheritdoc/>
    public async Task PostOutbound(CloudEvent cloudEvent, CancellationToken cancellationToken, bool useAzureServiceBus = false)
    {
        List<Subscription> subscriptions = await _subscriptionRepository.GetSubscriptions(
             cloudEvent.GetResource(),
             cloudEvent.Subject,
             cloudEvent.Type,
             cancellationToken);

        await AuthorizeAndPush(cloudEvent, subscriptions, cancellationToken, useAzureServiceBus);
    }

    /// <summary>
    /// Authorizes all distinct consumers for the cloud event and enqueues the event for each authorized subscription.
    /// Uses multi-decision authorization to check all consumers in a single request, then applies results to individual subscriptions.
    /// </summary>
    /// <param name="cloudEvent">The cloud event to be pushed.</param>
    /// <param name="subscriptions">List of subscriptions matching the event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="useAzureServiceBus">Indicates whether to use Azure Service Bus for event delivery.</param>
    private async Task AuthorizeAndPush(
        CloudEvent cloudEvent, List<Subscription> subscriptions, CancellationToken cancellationToken, bool useAzureServiceBus)
    {
        // Get distinct consumers to minimize authorization requests
        var consumers = subscriptions.Select(x => x.Consumer).Distinct().ToList();

        // Authorize all consumers in a single multi-decision request (with caching)
        var authorizationMultiResult = await Authorize(cloudEvent, consumers, cancellationToken);

        // Apply authorization results to each subscription
        foreach (Subscription subscription in subscriptions)
        {
            bool isAuthorized = authorizationMultiResult.TryGetValue(subscription.Consumer, out bool authorizedValue) && authorizedValue;

            if (!authorizationMultiResult.ContainsKey(subscription.Consumer))
            {
                _logger.LogWarning("No authorization result found for consumer {Consumer} on event {EventId}", subscription.Consumer, cloudEvent.Id);
            }

            await EnqueueOutbound(cloudEvent, subscription, authorized: isAuthorized, useAzureServiceBus);
        }
    }

    /// <summary>
    /// Authorizes multiple consumers for a cloud event by checking cache first, then calling authorization service for uncached consumers.
    /// Handles both Altinn App events and generic events.
    /// </summary>
    /// <param name="cloudEvent">The cloud event being authorized.</param>
    /// <param name="consumers">List of distinct consumer identifiers to authorize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping each consumer to their authorization status (true if authorized).</returns>
    private async Task<Dictionary<string, bool>> Authorize(CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        var consumersWithoutAuthorizationResult = new List<string>();
        var consumersWithCachedAuthorizationResult = new Dictionary<string, bool>();

        // Determine event type and get appropriate cache key mappings
        bool isAppEvent = IsAppEvent(cloudEvent);
        var mappedItems = isAppEvent
            ? GetAltinnAppAuthorizationCacheKeys(GetSourceFilter(cloudEvent.Source).ToString(), consumers)
            : GetAuthorizationCacheKeys(cloudEvent.GetResource(), consumers);

        // Check cache for each consumer
        foreach (var consumerCacheMappedItem in mappedItems)
        {
            if (!_memoryCache.TryGetValue(consumerCacheMappedItem.CacheKey, out bool isAuthorized))
            {
                // Cache miss - add to list for authorization
                consumersWithoutAuthorizationResult.Add(consumerCacheMappedItem.Consumer);
            }
            else
            {
                // Cache hit - use cached result
                consumersWithCachedAuthorizationResult.Add(consumerCacheMappedItem.Consumer, isAuthorized);
            }
        }

        // Authorize uncached consumers and cache the results
        Dictionary<string, bool> authorizationResult = await AuthorizeOrCacheMultipleConsumersForCloudEvent(cloudEvent, consumersWithoutAuthorizationResult, mappedItems, isAppEvent, cancellationToken);

        // Merge cached and newly authorized results
        return MergeDictionaries(consumersWithCachedAuthorizationResult, authorizationResult);
    }

    /// <summary>
    /// Merges cached authorization results with newly obtained authorization results.
    /// If a key exists in both dictionaries, the value from authorizationResult takes precedence.
    /// </summary>
    /// <param name="cachedConsumers">Authorization results retrieved from cache.</param>
    /// <param name="authorizationResult">Newly obtained authorization results.</param>
    /// <returns>Merged dictionary containing all authorization results.</returns>
    private static Dictionary<string, bool> MergeDictionaries(Dictionary<string, bool> cachedConsumers, Dictionary<string, bool> authorizationResult)
    {
        var merged = new Dictionary<string, bool>(cachedConsumers);
        foreach (var kvp in authorizationResult)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    /// <summary>
    /// Authorizes multiple consumers for a cloud event and caches the results.
    /// Routes to the appropriate authorization method based on event type (App event vs Generic event).
    /// Returns empty dictionary if no consumers need authorization.
    /// </summary>
    /// <param name="cloudEvent">The cloud event being authorized.</param>
    /// <param name="unauthorizedConsumers">List of consumers that were not found in cache.</param>
    /// <param name="cacheKeys">Mapping of consumers to their cache keys for storing results.</param>
    /// <param name="isAppEvent">True if this is an Altinn App event, false for generic events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping consumers to their authorization status.</returns>
    private async Task<Dictionary<string, bool>> AuthorizeOrCacheMultipleConsumersForCloudEvent(
        CloudEvent cloudEvent, 
        List<string> unauthorizedConsumers, 
        List<ConsumerCacheMap> cacheKeys,
        bool isAppEvent,
        CancellationToken cancellationToken)
    {
        Dictionary<string, bool> authorizationResult = [];

        if (unauthorizedConsumers.Count == 0)
        {
            return authorizationResult;
        }

        if (isAppEvent)
        {
            // App events are authorized for action "read" on app resource "events"
            authorizationResult = await _authorizationService.AuthorizeMultipleConsumersForAltinnAppEvent(cloudEvent, unauthorizedConsumers);
        }
        else
        {
            // Generic events use "subscribe" action for authorization
            authorizationResult = await _authorizationService.AuthorizeMultipleConsumersForGenericEvent(cloudEvent, unauthorizedConsumers, cancellationToken);
        }
            
        CacheResults(authorizationResult, cacheKeys);
        return authorizationResult;
    }

    /// <summary>
    /// Stores authorization results in memory cache using the appropriate cache keys for each consumer.
    /// Logs a warning if a consumer's cache key cannot be found.
    /// </summary>
    /// <param name="authorizationResult">Dictionary of authorization results keyed by consumer identifier.</param>
    /// <param name="cacheKeys">List of consumer-to-cache-key mappings.</param>
    private void CacheResults(Dictionary<string, bool> authorizationResult, List<ConsumerCacheMap> cacheKeys)
    {
        foreach (var result in authorizationResult)
        {
            var cacheKey = cacheKeys.FirstOrDefault(x => x.Consumer == result.Key)?.CacheKey;
            if (cacheKey == null)
            {
                _logger.LogWarning("No cache key found for consumer {Consumer} when trying to cache authorization result.", result.Key);
                continue;
            }

            _memoryCache.Set(cacheKey, result.Value, _consumerAuthorizationEntryOptions);
        }
    }

    /// <summary>
    /// Enqueues a cloud event for outbound delivery if the consumer is authorized.
    /// For unauthorized consumers, logs the unauthorized attempt and records telemetry.
    /// </summary>
    /// <param name="cloudEvent">The cloud event to enqueue.</param>
    /// <param name="subscription">The subscription containing consumer and endpoint information.</param>
    /// <param name="authorized">Whether the consumer is authorized to receive this event.</param>
    /// <param name="useAzureServiceBus">Indicates whether to use Azure Service Bus for event delivery.</param>
    private async Task EnqueueOutbound(
        CloudEvent cloudEvent, Subscription subscription, bool authorized, bool useAzureServiceBus)
    {
        if (authorized)
        {
            CloudEventEnvelope cloudEventEnvelope = MapToEnvelope(cloudEvent, subscription);

            if (useAzureServiceBus)
            {
                await _bus.PublishAsync(new OutboundEventCommand(cloudEventEnvelope));
            }
            else
            {
                var wrapper = new RetryableEventWrapper
                {
                    Payload = cloudEventEnvelope.Serialize()
                };

                var receipt = await _queueClient.EnqueueOutbound(wrapper.Serialize());

                if (!receipt.Success)
                {
                    _logger.LogError(receipt.Exception, "OutboundService EnqueueOutbound Failed to send event envelope {EventId} to consumer with {SubscriptionId}.", cloudEvent.Id, subscription.Id);
                }
            }

            await _traceLogService.CreateLogEntryWithSubscriptionDetails(cloudEvent, subscription, TraceLogActivity.OutboundQueue);
        }
        else
        {
            // Log unauthorized attempt and record telemetry
            await _traceLogService.CreateLogEntryWithSubscriptionDetails(cloudEvent, subscription, TraceLogActivity.Unauthorized);
            _telemetry?.SubscriptionAuthFailed();
        }
    }

    private static CloudEventEnvelope MapToEnvelope(CloudEvent cloudEvent, Subscription subscription)
    {
        CloudEventEnvelope cloudEventEnvelope = new()
        {
            CloudEvent = cloudEvent,
            Consumer = subscription.Consumer,
            Pushed = DateTime.UtcNow,
            SubscriptionId = subscription.Id,
            Endpoint = subscription.EndPoint
        };

        return cloudEventEnvelope;
    }

    private static List<ConsumerCacheMap> GetAuthorizationCacheKeys(string resource, List<string> consumers)
    {
        var cacheKeys = new List<ConsumerCacheMap>();

        foreach (var consumer in consumers)
        {
            cacheKeys.Add(new ConsumerCacheMap
            {
                Consumer = consumer,
                CacheKey = GetAuthorizationCacheKey(resource, consumer)
            });
        }

        return cacheKeys;
    }

    private static List<ConsumerCacheMap> GetAltinnAppAuthorizationCacheKeys(string sourceFilter, List<string> consumers)
    {
        var cacheKeys = new List<ConsumerCacheMap>();

        foreach (var consumer in consumers)
        {
            cacheKeys.Add(new ConsumerCacheMap
            {
                Consumer = consumer,
                CacheKey = GetAltinnAppAuthorizationCacheKey(sourceFilter, consumer)
            });
        }

        return cacheKeys;
    }

    private static string GetAuthorizationCacheKey(string resource, string consumer)
    {
        // Generic events always use "subscribe" action
        return $"authorizationdecision:re:{resource}:co:{consumer}:ac:subscribe";
    }

    private static string GetAltinnAppAuthorizationCacheKey(string sourceFilter, string consumer)
    {
        // App events always use "read" action
        return $"authorizationdecision:so:{sourceFilter}:co:{consumer}:ac:read";
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

    private static bool IsAppEvent(CloudEvent cloudEvent)
    {
        return cloudEvent.GetResource().StartsWith(AuthorizationConstants.AppResourcePrefix);
    }
}
