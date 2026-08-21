using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Wolverine.Policies;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Wolverine.Policies;

/// <summary>
/// A collection of tests related to <see cref="ValidationEventHandlerPolicy"/>.
/// </summary>
public class ValidationEventHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsInvalidOperationException()
    {
        var policy = new ValidationEventHandlerPolicy(new FunctionsWolverineSettings());

        Assert.Throws<InvalidOperationException>(() => policy.Apply([], null!, null!));
    }
}
