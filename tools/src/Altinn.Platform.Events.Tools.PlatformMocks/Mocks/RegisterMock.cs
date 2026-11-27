using System.Collections.Concurrent;

using Altinn.Platform.Events.Tools.PlatformMocks.Configuration;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Platform.Events.Tools.PlatformMocks.Mocks;

/// <summary>
/// The Altinn Register endpoints the events API calls. Every organisation and person exists. The parties in
/// <see cref="RegisterMockSettings.KnownParties"/> keep their ids. Other organisations get <c>500000000 + organisation number</c>,
/// so a party id can be turned back into the organisation, and other persons a stable id from their national identity number.
/// </summary>
internal sealed class RegisterMock(RegisterMockSettings settings) : IPlatformMock
{
    internal const string LookupPath = "/register/api/v1/parties/lookup";
    internal const string QueryPath = "/register/api/v2/internal/parties/query";
    internal const string MainUnitsPath = "/register/api/v2/internal/parties/main-units";

    private const string PartyIdUrn = "urn:altinn:party:id:";
    private const string OrganizationUrn = "urn:altinn:organization:identifier-no:";
    private const string PersonUrn = "urn:altinn:person:identifier-no:";

    // Persons looked up by national identity number, so a later lookup by their party id finds them.
    private readonly ConcurrentDictionary<int, string> _persons = new();

    public string System => "register";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(LookupPath).UsingPost())
            .RespondWith(Json(request => Lookup(MockJson.Read<PartyLookup>(request.Body))));

        server
            .Given(Request.Create().WithPath(QueryPath).UsingPost())
            .RespondWith(Json(request => new { Data = MockJson.Read<PartiesQuery>(request.Body).Data.Select(Identifiers).OfType<object>() }));

        server
            .Given(Request.Create().WithPath(MainUnitsPath).UsingPost())
            .RespondWith(Json(request => new { Data = MainUnits(MockJson.Read<MainUnitLookup>(request.Body).Data) }));
    }

    private static IResponseBuilder Json(Func<WireMock.IRequestMessage, object> body)
    {
        return Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(request => MockJson.Write(body(request)));
    }

    private object Lookup(PartyLookup lookup)
    {
        if (lookup.OrgNo is { Length: > 0 } organizationNumber)
        {
            return new { PartyId = OrganizationPartyId(organizationNumber), OrgNumber = organizationNumber, Name = $"Testorganisasjon {organizationNumber} AS" };
        }

        string personIdentifier = lookup.Ssn ?? string.Empty;
        return new { PartyId = PersonPartyId(personIdentifier), Ssn = personIdentifier, Name = $"Testperson {personIdentifier}" };
    }

    private object? Identifiers(string urn)
    {
        if (urn.StartsWith(OrganizationUrn, StringComparison.OrdinalIgnoreCase))
        {
            return Organization(urn[OrganizationUrn.Length..]);
        }

        if (urn.StartsWith(PersonUrn, StringComparison.OrdinalIgnoreCase))
        {
            return Person(urn[PersonUrn.Length..]);
        }

        if (!urn.StartsWith(PartyIdUrn, StringComparison.OrdinalIgnoreCase) || !int.TryParse(urn[PartyIdUrn.Length..], out int partyId))
        {
            return null;
        }

        if (settings.KnownParties.Find(p => p.PartyId == partyId) is { } known)
        {
            return known.OrganizationNumber is { } knownOrganization ? Organization(knownOrganization) : Person(known.PersonIdentifier ?? string.Empty);
        }

        if (partyId >= RegisterMockSettings.OrganizationPartyIdOffset)
        {
            return Organization((partyId - RegisterMockSettings.OrganizationPartyIdOffset).ToString("D9"));
        }

        // Not found, like Register, which leaves out the parties it does not know.
        return _persons.TryGetValue(partyId, out string? personIdentifier) ? Person(personIdentifier) : null;
    }

    private IEnumerable<object> MainUnits(string? organizationUrn)
    {
        string organizationNumber = (organizationUrn ?? string.Empty).Split(':')[^1];
        if (!settings.MainUnits.TryGetValue(organizationNumber, out string? mainUnit))
        {
            return [];
        }

        return [new { OrganizationIdentifier = mainUnit, ExternalUrn = OrganizationUrn + mainUnit, PartyId = OrganizationPartyId(mainUnit) }];
    }

    private object Organization(string organizationNumber) => new
    {
        PartyType = "organization",
        PartyUuid = MockJson.StableUuid("organization:" + organizationNumber),
        PartyId = OrganizationPartyId(organizationNumber),
        OrganizationIdentifier = organizationNumber,
    };

    private object Person(string personIdentifier) => new
    {
        PartyType = "person",
        PartyUuid = MockJson.StableUuid("person:" + personIdentifier),
        PartyId = PersonPartyId(personIdentifier),
        PersonIdentifier = personIdentifier,
    };

    private int OrganizationPartyId(string organizationNumber)
    {
        return settings.KnownParties.Find(p => p.OrganizationNumber == organizationNumber)?.PartyId
            ?? (int.TryParse(organizationNumber, out int number) ? RegisterMockSettings.OrganizationPartyIdOffset + number : MockJson.StableId(organizationNumber, 60_000_000));
    }

    private int PersonPartyId(string personIdentifier)
    {
        int partyId = settings.KnownParties.Find(p => p.PersonIdentifier == personIdentifier)?.PartyId ?? MockJson.StableId(personIdentifier, 50_000_000);
        _persons[partyId] = personIdentifier;
        return partyId;
    }

    private sealed class PartyLookup
    {
        public string? Ssn { get; set; }

        public string? OrgNo { get; set; }
    }

    private sealed class PartiesQuery
    {
        public List<string> Data { get; set; } = [];
    }

    private sealed class MainUnitLookup
    {
        public string? Data { get; set; }
    }
}
