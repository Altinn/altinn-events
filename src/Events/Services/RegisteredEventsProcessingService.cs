#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.BackgroundServices;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Claims and processes a single registered event per call to <see cref="TryProcessEvent"/>,
/// used by <see cref="RegisteredEventsBackgroundService"/>.
/// </summary>
public class RegisteredEventsProcessingService(
    ICloudEventRepository cloudEventRepository,
    IUnitOfWorkRepository unitOfWorkRepository,
    IOutboundService outboundService,
    ILogger<RegisteredEventsProcessingService> logger) : IRegisteredEventsProcessingService
{
    private static readonly ActivitySource _activitySource = new("Altinn.Platform.Events.RegisteredEventsProcessingService");

    /// <inheritdoc/>
    public async Task<bool> TryProcessEvent(CancellationToken cancellationToken)
    {
        using Activity? activity = _activitySource.StartActivity("TryProcessEvent");

        UnitOfWork unitOfWork;
        try
        {
            using (_activitySource.StartActivity("StartUnitOfWork"))
            {
                unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
            }
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(e, "// RegisteredEventsProcessingService // TryProcessEvent // Failed to start a unit of work.");
            }

            return false;
        }

        ClaimedEvent? claimedEvent = null;

        try
        {
            using (_activitySource.StartActivity("ClaimEvent"))
            {
                claimedEvent = await cloudEventRepository.ClaimRegisteredEventAsync(unitOfWork, cancellationToken);
            }

            activity?.SetTag("Claimed", claimedEvent != null);

            if (claimedEvent == null)
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            activity?.SetTag("EventId", claimedEvent.CloudEvent.Id);

            using (_activitySource.StartActivity("PostOutbound"))
            {
                await outboundService.PostOutbound(claimedEvent.CloudEvent, cancellationToken, true);
            }

            using (_activitySource.StartActivity("MarkEventProcessed"))
            {
                await cloudEventRepository.MarkEventProcessedAsync(unitOfWork, claimedEvent.SequenceNo, cancellationToken);
            }

            using (_activitySource.StartActivity("CommitUnitOfWork"))
            {
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
            }

            return true;
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    e,
                    "// RegisteredEventsProcessingService // TryProcessEvent // Failed to process claimed event {EventId}: {ErrorMessage}",
                    claimedEvent?.CloudEvent?.Id,
                    e.Message);
            }

            // Roll back everything, including the claim lock, before recording the retry. Updating a row that
            // was locked earlier in the same transaction from inside a savepoint creates a MultiXact per event,
            // which caused heavy MultiXact SLRU contention under load.
            await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);

            if (claimedEvent != null)
            {
                await MarkRetryInNewUnitOfWork(claimedEvent, e.Message, cancellationToken);
            }

            return false;
        }
    }

    private async Task MarkRetryInNewUnitOfWork(ClaimedEvent claimedEvent, string? retryReason, CancellationToken cancellationToken)
    {
        UnitOfWork retryUnitOfWork;
        try
        {
            retryUnitOfWork = await unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception startEx)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    startEx,
                    "// RegisteredEventsProcessingService // TryProcessEvent // Failed to start a unit of work to mark retry for event {EventId}: {ErrorMessage}",
                    claimedEvent.CloudEvent?.Id,
                    startEx.Message);
            }

            return;
        }

        try
        {
            // Record the failed attempt so retrycount/lastretried/retryreason are updated and
            // the event is marked 'retryExhausted' once MaxRetryCount is reached; otherwise
            // it stays 'registered' so it (or another task) can retry it on a future poll.
            await cloudEventRepository.MarkEventRetryAsync(retryUnitOfWork, claimedEvent.SequenceNo, retryReason, cancellationToken);
            await unitOfWorkRepository.CommitUnitOfWork(retryUnitOfWork);
        }
        catch (Exception retryEx)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    retryEx,
                    "// RegisteredEventsProcessingService // TryProcessEvent // Failed to mark retry for event {EventId}: {ErrorMessage}",
                    claimedEvent.CloudEvent?.Id,
                    retryEx.Message);
            }

            await unitOfWorkRepository.RollbackUnitOfWork(retryUnitOfWork);
        }
    }
}
