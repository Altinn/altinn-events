namespace Altinn.Platform.Events.Tools.PlatformMocks.Configuration;

/// <summary>
/// Settings for the local mocks of the Altinn platform APIs the events API calls, and of the subscribers it pushes events to.
/// </summary>
public class PlatformMocksSettings
{
    public RegisterMockSettings Register { get; set; } = new() { Port = 5082 };

    public AuthorizationMockSettings Authorization { get; set; } = new() { Port = 5083 };

    public WebhooksMockSettings Webhooks { get; set; } = new() { Port = 5084 };
}

/// <summary>
/// Settings every mock server has.
/// </summary>
public class MockServerSettings
{
    /// <summary>
    /// Whether the server is started at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The localhost port the server listens on.
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// Settings for the Register mock. Every organisation and person it is asked about exists. An organisation's party id is
/// <see cref="OrganizationPartyIdOffset"/> plus its organisation number, so it can be found from either.
/// </summary>
public class RegisterMockSettings : MockServerSettings
{
    /// <summary>
    /// Added to an organisation number to make its party id.
    /// </summary>
    public const int OrganizationPartyIdOffset = 500_000_000;

    /// <summary>
    /// Parties with fixed ids, e.g. the ones in the Bruno test data, which use the party ids from tt02.
    /// </summary>
    public List<KnownParty> KnownParties { get; set; } = [];

    /// <summary>
    /// Subunit organisation number -> main unit organisation number. Other organisations have no main unit.
    /// </summary>
    public Dictionary<string, string> MainUnits { get; set; } = [];
}

/// <summary>
/// A party with a fixed party id.
/// </summary>
public class KnownParty
{
    public int PartyId { get; set; }

    public string? OrganizationNumber { get; set; }

    public string? PersonIdentifier { get; set; }
}

/// <summary>
/// Settings for the Authorization mock, which permits everything except the subjects listed here.
/// </summary>
public class AuthorizationMockSettings : MockServerSettings
{
    /// <summary>
    /// Subject attribute values that get <c>Deny</c>, e.g. a user id or an org code like <c>ttd</c>.
    /// </summary>
    public List<string> DeniedSubjects { get; set; } = [];
}

/// <summary>
/// Settings for the webhook receiver. The events API only accepts https endpoints, so it serves https with a certificate the
/// API trusts, e.g. the ASP.NET Core development certificate.
/// </summary>
public class WebhooksMockSettings : MockServerSettings
{
    /// <summary>
    /// A PKCS#12 file with the server certificate and its key.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// How long <c>/webhooks/slow/...</c> waits before it answers.
    /// </summary>
    public int SlowSeconds { get; set; } = 10;
}
