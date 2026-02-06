#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Azure.Messaging.ServiceBus;

namespace Altinn.Platform.Events.IntegrationTests.Utils;

/// <summary>
/// Utility methods for working with Azure Service Bus in integration tests.
/// </summary>
public static class ServiceBusTestUtils
{
    /// <summary>
    /// Waits for a message to arrive on the specified queue and completes it.
    /// </summary>
    public static async Task<ServiceBusReceivedMessage?> WaitForMessageAsync(
        IntegrationTestContainersFixture fixture,
        string queueName,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var client = new ServiceBusClient(fixture.ServiceBusConnectionString);

        await using var receiver = client.CreateReceiver(queueName);
        using var cts = new CancellationTokenSource(actualTimeout);

        try
        {
            var message = await receiver.ReceiveMessageAsync(actualTimeout, cts.Token);

            if (message != null)
            {
                await receiver.CompleteMessageAsync(message, cts.Token);
            }

            return message;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// Waits for a message to arrive on the dead letter queue.
    /// </summary>
    public static Task<ServiceBusReceivedMessage?> WaitForDeadLetterMessageAsync(
        IntegrationTestContainersFixture fixture,
        string queueName,
        TimeSpan? timeout = null)
        => WaitForMessageAsync(fixture, $"{queueName}/$deadletterqueue", timeout);

    /// <summary>
    /// Waits until the specified queue is empty (no messages waiting).
    /// </summary>
    public static async Task<bool> WaitForEmptyAsync(
        IntegrationTestContainersFixture fixture,
        string queueName,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var pollInterval = TimeSpan.FromMilliseconds(100);
        var maxAttempts = (int)(actualTimeout.TotalMilliseconds / pollInterval.TotalMilliseconds);

        var client = new ServiceBusClient(fixture.ServiceBusConnectionString);
        await using var receiver = client.CreateReceiver(queueName);

        try
        {
            return await WaitForUtils.WaitForAsync(
                async () => await receiver.PeekMessageAsync() == null,
                maxAttempts,
                (int)pollInterval.TotalMilliseconds);
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// Waits until the dead letter queue is empty.
    /// </summary>
    public static Task<bool> WaitForDeadLetterEmptyAsync(
        IntegrationTestContainersFixture fixture,
        string queueName,
        TimeSpan? timeout = null)
        => WaitForEmptyAsync(fixture, $"{queueName}/$deadletterqueue", timeout);
}
