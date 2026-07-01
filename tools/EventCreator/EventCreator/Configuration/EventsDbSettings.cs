namespace EventCreator.Configuration;

/// <summary>
/// Settings for the Events Postgres database
/// </summary>
public class EventsDbSettings
{
    /// <summary>
    /// Connection string for the Events postgres db
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Command timeout in seconds. Defaults to 360 (6 minutes).
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 360;
}
