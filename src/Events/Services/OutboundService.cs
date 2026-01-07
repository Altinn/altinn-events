using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        await Authorize(cloudEvent, consumers, cancellationToken);

        foreach (Subscription subscription in subscriptions)
        {
            await AuthorizeAndEnqueueOutbound(cloudEvent, subscription, cancellationToken);
        }
    }

    private async Task Authorize(CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        if (IsAppEvent(cloudEvent))
        {
            var allAuthorized = await AuthorizeMultipleConsumersForAltinnAppEvent(cloudEvent, consumers, cancellationToken);
        }
        else
        {
            var allAuthorized = await AuthorizeMultipleConsumersForGenericEvent(cloudEvent, consumers, cancellationToken);
        }
    }

    private async Task<Dictionary<string, bool>> AuthorizeMultipleConsumersForGenericEvent(CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        var results = await _authorizationService.AuthorizeMultipleConsumersForGenericEvent(cloudEvent, consumers, cancellationToken);
        return results;
    }

    private async Task AuthorizeAndEnqueueOutbound(
        CloudEvent cloudEvent, Subscription subscription, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var authorized =
            IsAppEvent(cloudEvent)
            ? await AuthorizeConsumerForAltinnAppEvent(cloudEvent, subscription.Consumer)
            : await AuthorizeConsumerForGenericEvent(cloudEvent, subscription.Consumer, cancellationToken);

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

        sw.Stop();
        _telemetry?.RecordSubscriptionEventProcessingDuration(sw.ElapsedMilliseconds);
    }

    private async Task<bool> AuthorizeConsumerForAltinnAppEvent(CloudEvent cloudEvent, string consumer)
    {
        string cacheKey = GetAltinnAppAuthorizationCacheKey(GetSourceFilter(cloudEvent.Source).ToString(), consumer);

        if (!_memoryCache.TryGetValue(cacheKey, out bool isAuthorized))
        {
            isAuthorized = await _authorizationService.AuthorizeConsumerForAltinnAppEvent(cloudEvent, consumer);
            _memoryCache.Set(cacheKey, isAuthorized, _consumerAuthorizationEntryOptions);
        }

        return isAuthorized;
    }

    private async Task<Dictionary<string, bool>> AuthorizeMultipleConsumersForAltinnAppEvent(
        CloudEvent cloudEvent, List<string> consumers, CancellationToken cancellationToken)
    {
        var authorizationResults = await _authorizationService.AuthorizeMultipleConsumersForAltinnAppEvent(cloudEvent, consumers, cancellationToken);
        return authorizationResults;
    }

    private async Task<bool> AuthorizeConsumerForGenericEvent(
        CloudEvent cloudEvent, string consumer, CancellationToken cancellationToken)
    {
        string cacheKey = GetAuthorizationCacheKey(cloudEvent.GetResource(), consumer);

        if (!_memoryCache.TryGetValue(cacheKey, out bool isAuthorized))
        {
            isAuthorized = await _authorizationService.AuthorizeConsumerForGenericEvent(
                cloudEvent, consumer, cancellationToken);
            _memoryCache.Set(cacheKey, isAuthorized, _consumerAuthorizationEntryOptions);
        }

        return isAuthorized;
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

    private static string GetAuthorizationCacheKey(string resource, string consumer)
    {
        return "authorizationdecision:re:" + resource + "co:" + consumer;
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

    private static bool IsAppEvent(CloudEvent cloudEvent)
    {
        return cloudEvent.GetResource().StartsWith(AuthorizationConstants.AppResourcePrefix);
    }
}
