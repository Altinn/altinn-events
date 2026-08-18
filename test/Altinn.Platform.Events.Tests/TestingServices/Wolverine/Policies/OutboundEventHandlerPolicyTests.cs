using System;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Policies;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Policies;

/// <summary>
/// A collection of tests related to <see cref="OutboundEventHandlerPolicy"/>.
/// </summary>
public class OutboundEventHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsInvalidOperationException()
    {
        var policy = new OutboundEventHandlerPolicy(new WolverineSettings());

        Assert.Throws<InvalidOperationException>(() => policy.Apply([], null!, null!));
    }
}
