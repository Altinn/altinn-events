using System;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Queues;

/// <summary>
/// Represents a delegate for sending a message to a queue with optional visibility timeout and time-to-live
/// settings.
/// </summary>
/// <remarks>This delegate is typically used to abstract the logic for sending messages to a queue,
/// allowing for customization of visibility timeout and time-to-live settings. Ensure that the <paramref
/// name="message"/> parameter is properly validated before invoking the delegate.</remarks>
/// <param name="message">The message to be sent to the queue. Cannot be null or empty.</param>
/// <param name="visibilityTimeout">An optional <see cref="TimeSpan"/> specifying the duration the message should remain invisible in the queue
/// after being sent. If null, the default visibility timeout of the queue is used.</param>
/// <param name="timeToLive">An optional <see cref="TimeSpan"/> specifying the duration the message should remain in the queue before
/// expiring. If null, the default time-to-live of the queue is used.</param>
/// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
/// <returns>A <see cref="Task"/> that represents the asynchronous operation of sending the message to the queue.</returns>
// Main queue send
public delegate Task QueueSendDelegate(
    string message,
    TimeSpan? visibilityTimeout = null,
    TimeSpan? timeToLive = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Represents a delegate for sending a message to a poison queue with optional visibility timeout and time-to-live
/// settings.
/// </summary>
/// <remarks>This delegate is typically used in scenarios where messages that cannot be processed are sent
/// to a poison queue for further inspection or handling. Ensure that the <paramref name="message"/> parameter is
/// properly formatted and adheres to the poison queue's requirements.</remarks>
/// <param name="message">The message to be sent to the poison queue. This parameter cannot be <see langword="null"/> or empty.</param>
/// <param name="visibilityTimeout">An optional <see cref="TimeSpan"/> specifying the duration the message will remain invisible in the queue after
/// being sent. If <see langword="null"/>, the default visibility timeout of the queue is used.</param>
/// <param name="timeToLive">An optional <see cref="TimeSpan"/> specifying the duration the message will remain in the queue before expiring.
/// If <see langword="null"/>, the default time-to-live of the queue is used.</param>
/// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
/// <returns>A <see cref="Task"/> that represents the asynchronous operation of sending the message to the poison queue.</returns>
public delegate Task PoisonQueueSendDelegate(
    string message,
    TimeSpan? visibilityTimeout = null,
    TimeSpan? timeToLive = null,
    CancellationToken cancellationToken = default);
