using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Telemetry;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// An implementation of the push service
/// </summary>
public class OutboundService : IOutboundService
{
    private readonly IEventsQueueClient _queueClient;
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
        ITraceLogService traceLogService,
        ISubscriptionRepository repository,
        IAuthorization authorizationService,
        IOptions<PlatformSettings> platformSettings,
        IMemoryCache memoryCache,
        ILogger<OutboundService> logger,
        TelemetryClient telemetry)
    {
        _queueClient = queueClient;
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
    public async Task PostOutbound(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        List<Subscription> subscriptions = await _subscriptionRepository.GetSubscriptions(
             cloudEvent.GetResource(),
             cloudEvent.Subject,
             cloudEvent.Type,
             cancellationToken);

        await AuthorizeAndPush(cloudEvent, subscriptions, cancellationToken);
    }

    private async Task AuthorizeAndPush(
        CloudEvent cloudEvent, List<Subscription> subscriptions, CancellationToken cancellationToken)
    {
        var consumers = subscriptions.Select(x => x.Consumer).Distinct().ToList();

        var authorizationResult = await Authorize(cloudEvent, consumers, cancellationToken);

        foreach (Subscription subscription in subscriptions)
        {
            bool isAuthorized = authorizationResult.TryGetValue(subscription.Consumer, out bool authorized) && authorized;
            await EnqueueOutbound(cloudEvent, subscription, authorized: isAuthorized);
        }
    }

    private async Task<Dictionary<string, bool>> Authorize(CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        var unauthorizedConsumers = new List<string>();
        var cachedConsumers = new Dictionary<string, bool>();

        if (IsAppEvent(cloudEvent))
        {
            var cacheKeys = GetAltinnAppAuthorizationCacheKeys(GetSourceFilter(cloudEvent.Source).ToString(), consumers);

            foreach (var consumer in cacheKeys.Keys)
            {
                if (!_memoryCache.TryGetValue(cacheKeys[consumer], out bool isAuthorized))
                {
                    unauthorizedConsumers.Add(consumer);
                }
                else
                {
                    cachedConsumers.Add(consumer, isAuthorized);
                }
            }

            Dictionary<string, bool> authorizationResult = await AuthorizeOrCacheMultipleConsumersForAltinnAppEvent(cloudEvent, unauthorizedConsumers, cacheKeys);

            return cachedConsumers
                .Concat(authorizationResult)
                .ToDictionary(x => x.Key, x => x.Value);
        }
        else
        {
            // generic event
            var cacheKeysGeneric = GetAuthorizationCacheKeys(cloudEvent.GetResource(), consumers);
            foreach (var consumer in cacheKeysGeneric.Keys)
            {
                if (!_memoryCache.TryGetValue(cacheKeysGeneric[consumer], out bool isAuthorized))
                {
                    unauthorizedConsumers.Add(consumer);
                }
                else
                {
                    cachedConsumers.Add(consumer, isAuthorized);
                }
            }

            Dictionary<string, bool> authorizationResult = await AuthorizeOrCacheMultipleConsumersForGenericEvent(cloudEvent, unauthorizedConsumers, cacheKeysGeneric, cancellationToken);

            return cachedConsumers
                .Concat(authorizationResult)
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }

    private async Task<Dictionary<string, bool>> AuthorizeOrCacheMultipleConsumersForGenericEvent(CloudEvent cloudEvent, List<string> unauthorizedConsumers, Dictionary<string, string> cacheKeysGeneric, CancellationToken cancellationToken)
    {
        if (unauthorizedConsumers.Count == 0)
        {
            return [];
        }

        var authorizationResult = await _authorizationService.AuthorizeMultipleConsumersForGenericEvent(cloudEvent, unauthorizedConsumers, cancellationToken);
        CacheResults(authorizationResult, cacheKeysGeneric);
        return authorizationResult;
    }

    private async Task<Dictionary<string, bool>> AuthorizeOrCacheMultipleConsumersForAltinnAppEvent(CloudEvent cloudEvent, List<string> unauthorizedConsumers, Dictionary<string, string> cacheKeysAppEvent)
    {
        if (unauthorizedConsumers.Count == 0)
        {
            return [];
        }

        var authorizationResult = await _authorizationService.AuthorizeMultipleConsumersForAltinnAppEvent(cloudEvent, unauthorizedConsumers);
        CacheResults(authorizationResult, cacheKeysAppEvent);
        return authorizationResult;
    }

    private void CacheResults(Dictionary<string, bool> authorizationResult, Dictionary<string, string> cacheKeys)
    {
        foreach (var result in authorizationResult)
        {
            if (cacheKeys.TryGetValue(result.Key, out string cacheKey))
            {
                _memoryCache.Set(cacheKey, result.Value, _consumerAuthorizationEntryOptions);
            }
        }
    }

    private async Task EnqueueOutbound(
        CloudEvent cloudEvent, Subscription subscription, bool authorized)
    {
        if (authorized)
        {
            CloudEventEnvelope cloudEventEnvelope = MapToEnvelope(cloudEvent, subscription);

            var wrapper = new RetryableEventWrapper
            {
                Payload = cloudEventEnvelope.Serialize()
            };

            var receipt = await _queueClient.EnqueueOutbound(wrapper.Serialize());

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "OutboundService EnqueueOutbound Failed to send event envelope {EventId} to consumer with {SubscriptionId}.", cloudEvent.Id, subscription.Id);
            }

            await _traceLogService.CreateLogEntryWithSubscriptionDetails(cloudEvent, subscription, TraceLogActivity.OutboundQueue); // log that entry was added to outbound queue
        }
        else 
        {
            // add unauthorized trace log entry
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
            Pushed = DateTime.Now,
            SubscriptionId = subscription.Id,
            Endpoint = subscription.EndPoint
        };

        return cloudEventEnvelope;
    }

    private static Dictionary<string, string> GetAuthorizationCacheKeys(string resource, List<string> consumers)
    {
        var cacheKeys = new Dictionary<string, string>();

        foreach (var consumer in consumers)
        {
            cacheKeys.Add(consumer, GetAuthorizationCacheKey(resource, consumer));
        }

        return cacheKeys;
    }

    private static Dictionary<string, string> GetAltinnAppAuthorizationCacheKeys(string sourceFilter, List<string> consumers)
    {
        var cacheKeys = new Dictionary<string, string>();

        foreach (var consumer in consumers)
        {
            cacheKeys.Add(consumer, GetAltinnAppAuthorizationCacheKey(sourceFilter, consumer));
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
        if (sourceFilter == null)
        {
            return null;
        }

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
