using System;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Policies;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Policies;

/// <summary>
/// A collection of tests related to <see cref="RegistrationEventHandlerPolicy"/>.
/// </summary>
public class RegistrationEventHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsInvalidOperationException()
    {
        var policy = new RegistrationEventHandlerPolicy(new WolverineSettings());

        Assert.Throws<InvalidOperationException>(() => policy.Apply([], null!, null!));
    }
}
