using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// This interface describes the public contract of a repository implementation for <see cref="CloudEvent"/>
/// </summary>
public interface ICloudEventRepository
{
    /// <summary>
    /// Creates a cloud event in the repository.
    /// </summary>
    /// <param name="cloudEvent">The json serialized cloud event</param>
    /// <param name="idempotencyKey">The idempotency key for the request</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>true if the cloud event was created successfully, otherwise false</returns>
    Task<bool> CreateEvent(string cloudEvent, Guid? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Calls a function to retrieve app cloud events based on query params
    /// </summary>
    Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, string subject, List<string> source, string resource, List<string> type, int size);

    /// <summary>
    /// Calls a function to retrieve cloud events based on query params
    /// </summary>
    Task<List<CloudEvent>> GetEvents(string resource, string after, string subject, string alternativeSubject, List<string> type, int size);

    /// <summary>
    /// Claims a single registered event for processing within <paramref name="unitOfWork"/>'s
    /// transaction, which holds the claimed row's lock until the caller commits or rolls back.
    /// </summary>
    /// <returns>The claimed event, or <see langword="null"/> if no registered event is available.</returns>
    Task<ClaimedEvent> ClaimRegisteredEventAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the given event as processed. Does not commit — the caller is responsible for
    /// committing (or rolling back) <paramref name="unitOfWork"/>.
    /// </summary>
    Task MarkEventProcessedAsync(UnitOfWork unitOfWork, long sequenceNo, CancellationToken cancellationToken);

    /// <summary>
    /// Increments the given event's retry count, sets <c>lastretried</c>, and flips it to
    /// <c>retryExhausted</c> if the configured maximum has been reached. Does not commit — the
    /// caller is responsible for committing (or rolling back) <paramref name="unitOfWork"/>.
    /// </summary>
    Task MarkEventRetryAsync(UnitOfWork unitOfWork, long sequenceNo, CancellationToken cancellationToken);
}
