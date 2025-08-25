using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.IntegrationTests;

/// <summary>
/// Indicates that a test method requires Azurite to be running locally in order to execute.
/// </summary>
/// <remarks>This attribute is used to conditionally skip tests that depend on Azurite, a local Azure Storage
/// emulator. The test will be skipped if the environment variable <c>ENABLE_AZURITE_TESTS</c> is not set to <see
/// langword="true"/>  (case-insensitive) or <c>1</c>.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresAzuriteFactAttribute : FactAttribute
{
    public RequiresAzuriteFactAttribute()
    {
        if (!AzuriteTestsEnabled())
        {
            Skip = "Skipped: These tests require Azurite running on a local machine.";
        }
    }

    private static bool AzuriteTestsEnabled()
    {
        var v = Environment.GetEnvironmentVariable("ENABLE_AZURITE_TESTS");
        return string.Equals(v, "1", StringComparison.Ordinal)
        || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }
}
