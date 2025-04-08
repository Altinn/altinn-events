#nullable enable

using System;
using System.Collections.Generic;

namespace Altinn.Platform.Events.Services;

/// <summary>
/// Data type used for querying parties in register based on URN strings.
/// </summary>
public record PartiesRegisterQueryResponse
{
    /// <summary>
    /// List of valid URN strings with party identifying values.
    /// </summary>
    public required List<PartyIdentifiers> Data { get; set; }
}

/// <summary>
/// Represents a party with all possible identifiers.
/// </summary>
public record PartyIdentifiers
{
    /// <summary>
    /// The type of party, either "person" or "organization".
    /// </summary>
    public required string PartyType { get; set; }

    /// <summary>
    /// The party's UUID value.
    /// </summary>
    public Guid PartyUuid { get; set; }

    /// <summary>
    /// The party's original party id (From Altinn 2).
    /// </summary>
    public int PartyId { get; set; }

    /// <summary>
    /// The party's national identity number if of type "person".
    /// </summary>
    public string? PersonIdentifier { get; set; }

    /// <summary>
    /// The party's organization number if of type "organization".
    /// </summary>
    public string? OrganizationIdentifier { get; set; }
}
