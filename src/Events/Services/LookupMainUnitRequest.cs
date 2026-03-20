#nullable enable

using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Request model for the lookup resource for main units
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LookupMainUnitRequest"/> class.
/// </remarks>
/// <param name="orgNumber">Organization Number of the organization to lookup parent units for</param>
public class LookupMainUnitRequest(string orgNumber)
{
    /// <summary>
    /// Data containing the urn of the organization with either orgNumber, partyId or PartyUuid.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; init; } = $"urn:altinn:organization:identifier-no:{orgNumber}";
}
