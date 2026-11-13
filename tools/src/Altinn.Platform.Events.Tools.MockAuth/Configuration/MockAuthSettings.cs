namespace Altinn.Platform.Events.Tools.MockAuth.Configuration;

/// <summary>
/// Settings for the local mock token issuer.
/// </summary>
public class MockAuthSettings
{
    /// <summary>
    /// The localhost port the issuer listens on.
    /// </summary>
    public int Port { get; set; } = 5081;

    /// <summary>
    /// Basic auth username for the token endpoints, like the Altinn test token generator.
    /// </summary>
    public string Username { get; set; } = "mock";

    /// <summary>
    /// Basic auth password for the token endpoints.
    /// </summary>
    public string Password { get; set; } = "mock";
}
