#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Response model for the lookup resource for main units
/// </summary>
public class LookupMainUnitResponse()
{
    /// <summary>
    /// Data containing the urn of the organization with either orgNumber, partyId or PartyUuid.
    /// </summary>
    [JsonPropertyName("data")]
    public List<OrganizationRecord> Data { get; init; } = [];
}
