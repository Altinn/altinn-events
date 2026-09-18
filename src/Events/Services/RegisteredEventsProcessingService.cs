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
            unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
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
            claimedEvent = await cloudEventRepository.ClaimRegisteredEventAsync(unitOfWork, cancellationToken);
            activity?.SetTag("Claimed", claimedEvent != null);

            if (claimedEvent == null)
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            activity?.SetTag("EventId", claimedEvent.CloudEvent.Id);

            await outboundService.PostOutbound(claimedEvent.CloudEvent, cancellationToken, true);
            await cloudEventRepository.MarkEventProcessedAsync(unitOfWork, claimedEvent.SequenceNo, cancellationToken);
            await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

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

            // Retry tracking (retrycount/lastretried/retryExhausted) is postponed; for now a
            // full rollback simply releases the claim, leaving the event as 'registered' so
            // it (or another task) can retry it on a future poll.
            await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            return false;
        }
    }
}
