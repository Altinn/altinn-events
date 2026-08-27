using Altinn.Platform.Events.Functions.Wolverine.Configuration;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Wolverine.Configuration;

/// <summary>
/// A collection of tests related to <see cref="QueueRetryPolicy"/>.
/// </summary>
public class QueueRetryPolicyTests
{
    [Fact]
    public void GetCooldownDelays_ConvertsMillisecondsToTimeSpans()
    {
        // Arrange
        var policy = new QueueRetryPolicy
        {
            CooldownDelaysMs = [1000, 5000, 10000]
        };

        // Act
        var actual = policy.GetCooldownDelays();

        // Assert
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)],
            actual);
    }

    [Fact]
    public void GetScheduleDelays_ConvertsMillisecondsToTimeSpans()
    {
        // Arrange
        var policy = new QueueRetryPolicy
        {
            ScheduleDelaysMs = [30000, 60000]
        };

        // Act
        var actual = policy.GetScheduleDelays();

        // Assert
        Assert.Equal(
            [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1)],
            actual);
    }

    [Fact]
    public void GetCooldownDelays_NoDelaysConfigured_ReturnsEmptyArray()
    {
        // Arrange
        var policy = new QueueRetryPolicy();

        // Act
        var actual = policy.GetCooldownDelays();

        // Assert
        Assert.Empty(actual);
    }
}
