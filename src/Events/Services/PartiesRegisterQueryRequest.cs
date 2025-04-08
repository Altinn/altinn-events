#nullable enable

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Data type used for querying parties in register based on URN strings.
/// </summary>
public record PartiesRegisterQueryRequest
{
    /// <summary>
    /// List of valid URN strings with party identifying values.
    /// </summary>
    public required string[] Data { get; set; }
}
