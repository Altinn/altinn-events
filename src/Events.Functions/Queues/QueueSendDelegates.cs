using System;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Functions.Queues;

/// <summary>
/// Delegate for sending messages to a queue.
/// </summary>
/// <param name="message">The message content to send.</param>
/// <param name="visibilityTimeout">Optional duration the message remains invisible in the queue after being added.</param>
/// <param name="timeToLive">Optional duration before the message expires in the queue.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A task representing the asynchronous send operation.</returns>
public delegate Task QueueSendDelegate(
    string message,
    TimeSpan? visibilityTimeout = null,
    TimeSpan? timeToLive = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Delegate for sending messages to a poison queue when they can't be processed normally.
/// </summary>
/// <param name="message">The message content to send to the poison queue.</param>
/// <param name="visibilityTimeout">Optional duration the message remains invisible in the queue after being added.</param>
/// <param name="timeToLive">Optional duration before the message expires in the queue.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A task representing the asynchronous send operation.</returns>
public delegate Task PoisonQueueSendDelegate(
    string message,
    TimeSpan? visibilityTimeout = null,
    TimeSpan? timeToLive = null,
    CancellationToken cancellationToken = default);
