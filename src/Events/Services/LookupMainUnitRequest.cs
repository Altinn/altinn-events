#nullable enable

using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Request model for the lookup resource for main units
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LookupMainUnitRequest"/> class.
/// </remarks>
/// <param name="organizationUrnValue">Any URN that uniquely identifies an organization.</param>
public class LookupMainUnitRequest(string organizationUrnValue)
{
    /// <summary>
    /// Data containing the urn of the organization with either orgNumber, partyId or PartyUuid.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; init; } = organizationUrnValue;
}
