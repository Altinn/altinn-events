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

            if (claimedEvent == null)
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            try
            {
                // Record the failed attempt so retrycount/lastretried are updated and the
                // event is marked 'retryExhausted' once MaxRetryCount is reached; otherwise
                // it stays 'registered' so it (or another task) can retry it on a future poll.
                await cloudEventRepository.MarkEventRetryAsync(unitOfWork, claimedEvent.SequenceNo, cancellationToken);
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
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

                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }

            return false;
        }
    }
}
