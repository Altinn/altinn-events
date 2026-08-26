using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Wolverine.Policies;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Wolverine.Policies;

/// <summary>
/// A collection of tests related to <see cref="OutboundEventHandlerPolicy"/>.
/// </summary>
public class OutboundEventHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsInvalidOperationException()
    {
        var policy = new OutboundEventHandlerPolicy(new FunctionsWolverineSettings());

        Assert.Throws<InvalidOperationException>(() => policy.Apply([], null!, null!));
    }
}
