#nullable enable

using System.Text.Json.Serialization;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// A simplified record for an organization.
/// </summary>
public record OrganizationRecord
{
    /// <summary>
    /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
    /// </summary>
    [JsonPropertyName("organizationIdentifier")]
    public string? OrganizationIdentifier { get; set; }

    /// <summary>
    /// Gets the external Uniform Resource Name (URN) associated with this entity.
    /// </summary>
    [JsonPropertyName("externalUrn")] 
    public string? ExternalUrn { get; set; }

    /// <summary>
    /// Gets the party ID of the organization, or 0 if the party is not an organization.
    /// </summary>
    [JsonPropertyName("partyId")]
    public int PartyId { get; set; }
}
