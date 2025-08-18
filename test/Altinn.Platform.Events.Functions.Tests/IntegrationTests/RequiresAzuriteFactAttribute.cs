using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.IntegrationTests;

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
