using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.IsolatedFunctions.Services;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests.IntegrationTests;

/// <summary>
/// Overrides the visibility timeout logic for integration tests.
/// </summary>
internal sealed class TestableRetryBackoffService: RetryBackoffService
{
    public TestableRetryBackoffService(
        ILogger<TestableRetryBackoffService> logger,
        QueueSendDelegate sendToQueue,
        PoisonQueueSendDelegate sendToPoison)
        : base(logger, sendToQueue, sendToPoison)
    {
    }

    /// <summary>
    /// Compressed visbility timeout based on dequeue count for integration tests.
    /// </summary>
    /// <param name="dequeueCount"></param>
    /// <returns></returns>
    public override TimeSpan GetVisibilityTimeout(int dequeueCount) => dequeueCount switch
    {
        1 => TimeSpan.FromSeconds(1),
        2 => TimeSpan.FromSeconds(2),
        3 => TimeSpan.FromSeconds(3),
        4 => TimeSpan.FromSeconds(4),
        5 => TimeSpan.FromSeconds(5),
        6 => TimeSpan.FromSeconds(6),
        7 => TimeSpan.FromSeconds(7),
        8 => TimeSpan.FromSeconds(8),
        9 => TimeSpan.FromSeconds(9),
        10 or 11 => TimeSpan.FromSeconds(10),
        _ => TimeSpan.FromSeconds(11)
    };
}
