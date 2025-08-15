namespace Altinn.Platform.Events.IsolatedFunctions.Tests.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresAzuriteFactAttribute : FactAttribute
{
    
    public RequiresAzuriteFactAttribute()
    {
        if (!IsDevelopmentEnvironment())
        {
            Skip = "Skipped: These tests require Azurite running on a local machine).";
        }
    }

    private static bool IsDevelopmentEnvironment()
    {
        // Check ASPNETCORE_ENVIRONMENT or a custom variable
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
               
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
