using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

/// <summary>
/// A collection of tests related to <see cref="RegisteredEventsProcessingService"/>.
/// </summary>
public class RegisteredEventsProcessingServiceTests
{
    private readonly Mock<ICloudEventRepository> _cloudEventRepositoryMock;
    private readonly Mock<IUnitOfWorkRepository> _unitOfWorkRepositoryMock;
    private readonly Mock<IOutboundService> _outboundServiceMock;
    private readonly Mock<ILogger<RegisteredEventsProcessingService>> _loggerMock;
    private readonly UnitOfWork _unitOfWork;

    public RegisteredEventsProcessingServiceTests()
    {
        _cloudEventRepositoryMock = new();
        _unitOfWorkRepositoryMock = new();
        _outboundServiceMock = new();
        _loggerMock = new();

        _unitOfWork = new UnitOfWork
        {
            Connection = new NpgsqlConnection(),
            Transaction = null!
        };

        _unitOfWorkRepositoryMock
            .Setup(u => u.StartUnitOfWork())
            .ReturnsAsync(_unitOfWork);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent is called but no registered event is available to claim.
    /// Expected result:
    ///   The unit of work is rolled back and false is returned.
    /// Success criteria:
    ///   RollbackUnitOfWork is called once; outbound delivery and commit are never attempted.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_NoRegisteredEventAvailable_RollsBackAndReturnsFalse()
    {
        // Arrange
        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimedEvent)null);

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        _outboundServiceMock.Verify(o => o.PostOutbound(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent claims a registered event and outbound delivery succeeds.
    /// Expected result:
    ///   The event is marked processed and the unit of work is committed.
    /// Success criteria:
    ///   PostOutbound, MarkEventProcessedAsync, and CommitUnitOfWork are all called once; true is returned.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_EventClaimedAndOutboundSucceeds_MarksProcessedAndCommits()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 42,
            CloudEvent = GetCloudEvent()
        };

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .Returns(Task.CompletedTask);

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.True(result);
        _outboundServiceMock.Verify(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true), Times.Once);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventProcessedAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent claims a registered event but outbound delivery throws.
    /// Expected result:
    ///   The failed attempt is recorded via MarkEventRetryAsync and the unit of work is
    ///   committed (not rolled back), so retrycount/lastretried are persisted. False is returned.
    /// Success criteria:
    ///   MarkEventRetryAsync and CommitUnitOfWork are called once each; MarkEventProcessedAsync
    ///   and RollbackUnitOfWork are never called.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_OutboundThrows_MarksRetryAndCommits()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 7,
            CloudEvent = GetCloudEvent()
        };

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .ThrowsAsync(new InvalidOperationException("Outbound delivery failed"));

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventProcessedAsync(It.IsAny<UnitOfWork>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent claims a registered event, outbound delivery fails, and the subsequent
    ///   attempt to record the retry (MarkEventRetryAsync) also throws.
    /// Expected result:
    ///   The unit of work falls back to a full rollback so the claim lock is released and the
    ///   event remains 'registered' for a future attempt. False is returned.
    /// Success criteria:
    ///   MarkEventRetryAsync is attempted once; CommitUnitOfWork is never called; RollbackUnitOfWork
    ///   is called once.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_OutboundThrowsAndMarkRetryThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 8,
            CloudEvent = GetCloudEvent()
        };

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .ThrowsAsync(new InvalidOperationException("Outbound delivery failed"));

        _cloudEventRepositoryMock
            .Setup(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
    }

    /// <summary>
    /// Scenario:
    ///   TryProcessEvent claims a registered event, outbound delivery succeeds, but marking
    ///   the event processed (the PostgreSQL update) throws.
    /// Expected result:
    ///   The transaction is rolled back to the 'event_claimed' savepoint (undoing only the
    ///   failed MarkEventProcessedAsync attempt, not the claim itself), the failed attempt is
    ///   then recorded via MarkEventRetryAsync, and the unit of work is committed. False is
    ///   returned.
    /// Success criteria:
    ///   RollbackUnitOfWorkToSavepoint occurs before MarkEventRetryAsync, which occurs before
    ///   CommitUnitOfWork; CommitUnitOfWork is called exactly once and RollbackUnitOfWork
    ///   (full rollback) is never called.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_MarkEventProcessedThrows_RollsBackToSavepointThenMarksRetryAndCommits()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 99,
            CloudEvent = GetCloudEvent()
        };

        var callOrder = new List<string>();

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .Returns(Task.CompletedTask);

        _cloudEventRepositoryMock
            .Setup(r => r.MarkEventProcessedAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        _unitOfWorkRepositoryMock
            .Setup(u => u.RollbackUnitOfWorkToSavepoint(_unitOfWork, "event_claimed"))
            .Callback(() => callOrder.Add(nameof(IUnitOfWorkRepository.RollbackUnitOfWorkToSavepoint)))
            .Returns(Task.CompletedTask);

        _cloudEventRepositoryMock
            .Setup(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add(nameof(ICloudEventRepository.MarkEventRetryAsync)))
            .Returns(Task.CompletedTask);

        _unitOfWorkRepositoryMock
            .Setup(u => u.CommitUnitOfWork(_unitOfWork))
            .Callback(() => callOrder.Add(nameof(IUnitOfWorkRepository.CommitUnitOfWork)))
            .Returns(Task.CompletedTask);

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Equal(
            [
                nameof(IUnitOfWorkRepository.RollbackUnitOfWorkToSavepoint),
                nameof(ICloudEventRepository.MarkEventRetryAsync),
                nameof(IUnitOfWorkRepository.CommitUnitOfWork)
            ],
            callOrder);

        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWorkToSavepoint(_unitOfWork, "event_claimed"), Times.Once);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    /// <summary>
    /// Scenario:
    ///   Starting the unit of work itself fails (e.g. connection unavailable).
    /// Expected result:
    ///   The error is logged and false is returned without attempting to claim an event.
    /// Success criteria:
    ///   ClaimRegisteredEventAsync is never called; an error is logged once.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_StartUnitOfWorkThrows_LogsErrorAndReturnsFalse()
    {
        // Arrange
        _unitOfWorkRepositoryMock
            .Setup(u => u.StartUnitOfWork())
            .ThrowsAsync(new InvalidOperationException("Connection unavailable"));

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        _cloudEventRepositoryMock.Verify(r => r.ClaimRegisteredEventAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyErrorLogged(Times.Once());
    }

    /// <summary>
    /// Scenario:
    ///   Starting the unit of work fails, but cancellation was already requested.
    /// Expected result:
    ///   False is returned without logging an error, since the failure is attributable to
    ///   cancellation rather than a genuine fault.
    /// Success criteria:
    ///   No error is logged.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_StartUnitOfWorkThrowsDuringCancellation_DoesNotLogError()
    {
        // Arrange
        _unitOfWorkRepositoryMock
            .Setup(u => u.StartUnitOfWork())
            .ThrowsAsync(new OperationCanceledException());

        var target = GetTarget();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        bool result = await target.TryProcessEvent(cts.Token);

        // Assert
        Assert.False(result);
        VerifyErrorLogged(Times.Never());
    }

    /// <summary>
    /// Scenario:
    ///   The unit of work starts successfully, but ClaimRegisteredEventAsync itself throws
    ///   (e.g. the claim query fails), so no event was ever claimed.
    /// Expected result:
    ///   The error is logged, a single full rollback occurs (not a savepoint rollback, since
    ///   there is no claim to preserve), and false is returned.
    /// Success criteria:
    ///   RollbackUnitOfWork is called once; RollbackUnitOfWorkToSavepoint, MarkEventRetryAsync,
    ///   and CommitUnitOfWork are never called.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_ClaimRegisteredEventThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Claim query failed"));

        var target = GetTarget();

        // Act
        bool result = await target.TryProcessEvent(CancellationToken.None);

        // Assert
        Assert.False(result);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWorkToSavepoint(It.IsAny<UnitOfWork>(), It.IsAny<string>()), Times.Never);
        _cloudEventRepositoryMock.Verify(r => r.MarkEventRetryAsync(It.IsAny<UnitOfWork>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        VerifyErrorLogged(Times.Once());
    }

    /// <summary>
    /// Scenario:
    ///   ClaimRegisteredEventAsync throws, but cancellation was already requested.
    /// Expected result:
    ///   False is returned and the unit of work is still rolled back, but no error is logged.
    /// Success criteria:
    ///   RollbackUnitOfWork is called once; no error is logged.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_ClaimRegisteredEventThrowsDuringCancellation_DoesNotLogError()
    {
        // Arrange
        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var target = GetTarget();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        bool result = await target.TryProcessEvent(cts.Token);

        // Assert
        Assert.False(result);
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        VerifyErrorLogged(Times.Never());
    }

    /// <summary>
    /// Scenario:
    ///   Outbound delivery fails while cancellation was already requested.
    /// Expected result:
    ///   The failure-path error is not logged, but the retry is still recorded and committed,
    ///   since retry accounting is not itself guarded by the cancellation check.
    /// Success criteria:
    ///   No error is logged for the processing failure; MarkEventRetryAsync and
    ///   CommitUnitOfWork are still called once each.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_OutboundThrowsDuringCancellation_DoesNotLogErrorButStillMarksRetry()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 15,
            CloudEvent = GetCloudEvent()
        };

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .ThrowsAsync(new OperationCanceledException());

        var target = GetTarget();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        bool result = await target.TryProcessEvent(cts.Token);

        // Assert
        Assert.False(result);
        VerifyErrorLogged(Times.Never());
        _cloudEventRepositoryMock.Verify(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(_unitOfWork), Times.Once);
    }

    /// <summary>
    /// Scenario:
    ///   MarkEventRetryAsync throws while cancellation was already requested.
    /// Expected result:
    ///   The retry-failure error is not logged, and the unit of work still falls back to a
    ///   full rollback.
    /// Success criteria:
    ///   No error is logged; RollbackUnitOfWork is called once; CommitUnitOfWork is never called.
    /// </summary>
    [Fact]
    public async Task TryProcessEvent_MarkEventRetryThrowsDuringCancellation_DoesNotLogError()
    {
        // Arrange
        ClaimedEvent claimedEvent = new()
        {
            SequenceNo = 16,
            CloudEvent = GetCloudEvent()
        };

        _cloudEventRepositoryMock
            .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedEvent);

        _outboundServiceMock
            .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
            .ThrowsAsync(new InvalidOperationException("Outbound delivery failed"));

        _cloudEventRepositoryMock
            .Setup(r => r.MarkEventRetryAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var target = GetTarget();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        bool result = await target.TryProcessEvent(cts.Token);

        // Assert
        Assert.False(result);
        VerifyErrorLogged(Times.Never());
        _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    private void VerifyErrorLogged(Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            times);
    }

    private RegisteredEventsProcessingService GetTarget()
    {
        return new RegisteredEventsProcessingService(
            _cloudEventRepositoryMock.Object,
            _unitOfWorkRepositoryMock.Object,
            _outboundServiceMock.Object,
            _loggerMock.Object);
    }

    private static CloudEvent GetCloudEvent()
    {
        return new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "instance.created",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/apps-test/"),
            Time = DateTimeOffset.Now,
            Subject = "/party/456456"
        };
    }
}
