using System;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Wolverine.Policies;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices.Wolverine.Policies;

/// <summary>
/// A collection of tests related to <see cref="ValidationEventHandlerPolicy"/>.
/// </summary>
public class ValidationEventHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsInvalidOperationException()
    {
        var policy = new ValidationEventHandlerPolicy(new WolverineSettings());

        Assert.Throws<InvalidOperationException>(() => policy.Apply([], null!, null!));
    }
}
