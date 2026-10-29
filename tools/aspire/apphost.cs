#:sdk Aspire.AppHost.Sdk@13.6.4
#:package Aspire.Hosting.PostgreSQL@13.6.4
#:package Aspire.Hosting.Azure.ServiceBus@13.6.4
#:package Aspire.Hosting.Azure.Functions@13.6.4
#:property AspireUseCliBundle=true

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

const string Root = "../..";

// The Key Vault SDK only talks to a vault whose host name ends with ".<resource in the vault's auth challenge>". The emulator
// answers with resource dev.localhost, and the ASP.NET Core development certificate covers *.dev.localhost.
const string VaultHost = "events.dev.localhost";

// Local only. The exported development certificate is protected with it on disk.
const string DevCertificatePassword = "altinn-events-local";

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile(Path.Combine(builder.AppHostDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(builder.AppHostDirectory, "appsettings.Local.json"), optional: true);

int Port(string name) => builder.Configuration.GetValue<int>($"Ports:{name}");

// The outbound and validation listeners on Service Bus run in the Functions app in the cluster, and move to the API (#1076).
bool InFunctions(string listener) => builder.Configuration.GetValue($"Listeners:{listener}", "Functions") switch
{
    "Functions" => true,
    "Api" => false,
    var other => throw new InvalidOperationException($"Listeners:{listener} is '{other}'. Use 'Functions' or 'Api'."),
};

bool outboundInFunctions = InFunctions("Outbound");
bool validationInFunctions = InFunctions("Validation");

string localDirectory = Path.Combine(builder.AppHostDirectory, ".local");
Directory.CreateDirectory(localDirectory);

CheckVaultHostResolves();
string devCertificatePath = ExportDevCertificate();
CopyAccessTokenSigningCertificate();

// The API itself. Its own settings point to localtest on 5101. Everything it calls is set here instead, so its appsettings stay as they are.
var api = builder.AddProject("api", $"{Root}/src/Events/Altinn.Platform.Events.csproj", launchProfileName: null)
    .WithHttpEndpoint(port: 5080, targetPort: 5080, isProxied: false)
    .WithUrl("http://localhost:5080/swagger", "Swagger")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    // The API caches subscription lookups and authorization decisions for an hour. Locally a new subscription should match the next event.
    .WithEnvironment("PlatformSettings__SubscriptionCachingLifetimeInSeconds", builder.Configuration.GetValue("Api:SubscriptionCachingLifetimeInSeconds", 5).ToString());

// Postgres with the roles and database from the README. Persistent, so the data and the container survive a restart of the app host.
var postgresPort = Port("Postgres");
var postgres = builder.AddPostgres(
        "postgres",
        builder.AddParameter("postgres-user", "platform_events_admin"),
        builder.AddParameter("postgres-password", "Password", secret: true),
        port: postgresPort)
    .WithDataVolume("altinn-events-aspire-db")
    .WithInitFiles(Path.Combine(builder.AppHostDirectory, "postgres"))
    .WithLifetime(ContainerLifetime.Persistent);

string connectionString = "Host=localhost;Port=" + postgresPort + ";Username={0};Password={{0}};Database=eventsdb";
api.WithEnvironment("PostgreSQLSettings__AdminConnectionString", string.Format(connectionString, "platform_events_admin"))
    .WithEnvironment("PostgreSQLSettings__ConnectionString", string.Format(connectionString, "platform_events") + ";Include Error Detail=True")
    .WaitFor(postgres);

// The Service Bus emulator with the queues from emulator/config.json.
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator
        .WithConfigurationFile(Path.Combine(builder.AppHostDirectory, Root, "emulator/config.json"))
        .WithLifetime(ContainerLifetime.Persistent));

api.WithEnvironment("WolverineSettings__ServiceBusConnectionString", serviceBus)
    .WithEnvironment("WolverineSettings__EnableOutboundListener", (!outboundInFunctions).ToString())
    .WithEnvironment("WolverineSettings__EnableValidationListener", (!validationInFunctions).ToString())
    .WaitFor(serviceBus);

// Lowkey Vault in place of Azure Key Vault, with the public certificates the API checks platform access tokens against, and the
// private certificate Functions signs with. DefaultAzureCredential gets its token from the emulator's managed identity endpoint.
var keyVaultPort = Port("KeyVault");
string keyVaultUrl = $"https://{VaultHost}:{keyVaultPort}/";
var keyVault = builder.AddContainer("keyvault", "docker.io/nagyesta/lowkey-vault", "7.3.127")
    .WithBindMount(localDirectory, "/certs", isReadOnly: true)
    .WithEnvironment("LOWKEY_ARGS", string.Join(' ',
        $"--server.port={keyVaultPort}",
        "--LOWKEY_VAULT_NAMES=events",
        $"--LOWKEY_VAULT_ALIASES=events.localhost={VaultHost}:<port>",
        "--LOWKEY_AUTH_RESOURCE=dev.localhost",
        "--server.ssl.key-store=/certs/devcert.pfx",
        "--server.ssl.key-store-type=PKCS12",
        $"--server.ssl.key-store-password={DevCertificatePassword}"))
    .WithEndpoint(port: keyVaultPort, targetPort: keyVaultPort, scheme: "https", name: "https", isProxied: false)
    .WithHttpEndpoint(port: Port("KeyVaultToken"), targetPort: 8080, name: "token", isProxied: false)
    .WithHttpHealthCheck("/metadata/identity/oauth2/token?resource=https://dev.localhost", endpointName: "token")
    .WithUrl(keyVaultUrl, "Vault");

builder.Eventing.Subscribe<ResourceReadyEvent>(keyVault.Resource, async (e, ct) =>
{
    var logger = e.Services.GetRequiredService<ResourceLoggerService>().GetLogger(keyVault.Resource);
    await SeedKeyVaultAsync(logger, ct);
});

string identityEndpoint = "http://localhost:" + Port("KeyVaultToken") + "/metadata/identity/oauth2/token";
api.WithEnvironment("kvSetting__SecretUri", keyVaultUrl)
    .WithEnvironment("IDENTITY_ENDPOINT", identityEndpoint)
    .WithEnvironment("IDENTITY_HEADER", "lowkey-vault")
    .WaitFor(keyVault);

// A local token issuer in place of the Altinn test token generator. The API trusts its bearer tokens through OpenID discovery,
// and its platform access tokens through the public certificates in the vault.
string authUrl = "http://localhost:" + Port("MockAuth");
var auth = builder.AddProject("mock-auth", $"{Root}/tools/src/Altinn.Platform.Events.Tools.MockAuth/Altinn.Platform.Events.Tools.MockAuth.csproj", launchProfileName: null)
    .WithEnvironment("MockAuthSettings__Port", Port("MockAuth").ToString())
    .WithUrl(authUrl + "/authentication/api/v1/openid/.well-known/openid-configuration", "OpenID configuration");

api.WithEnvironment("GeneralSettings__OpenIdWellKnownEndpoint", authUrl + "/authentication/api/v1/openid/")
    .WaitFor(auth);

// Register and Authorization, and a webhook receiver to use as subscription endpoint. The API only accepts https endpoints, so
// the receiver serves https with the development certificate.
string webhooksUrl = $"https://localhost:{Port("Webhooks")}/webhooks/";
var mocks = builder.AddProject("wiremock-platform", $"{Root}/tools/src/Altinn.Platform.Events.Tools.PlatformMocks/Altinn.Platform.Events.Tools.PlatformMocks.csproj", launchProfileName: null)
    .WithEnvironment("PlatformMocksSettings__Register__Port", Port("Register").ToString())
    .WithEnvironment("PlatformMocksSettings__Authorization__Port", Port("Authorization").ToString())
    .WithEnvironment("PlatformMocksSettings__Webhooks__Port", Port("Webhooks").ToString())
    .WithEnvironment("PlatformMocksSettings__Webhooks__CertificatePath", devCertificatePath)
    .WithEnvironment("PlatformMocksSettings__Webhooks__CertificatePassword", DevCertificatePassword)
    .WithUrl(webhooksUrl + "ok", "Webhook (ok)")
    .WithUrl(webhooksUrl + "fail", "Webhook (fail)");

api.WithEnvironment("PlatformSettings__RegisterApiBaseAddress", "http://localhost:" + Port("Register") + "/register/api/")
    .WithEnvironment("PlatformSettings__ApiAuthorizationEndpoint", "http://localhost:" + Port("Authorization") + "/authorization/api/v1/")
    .WaitFor(mocks);

// The Functions app, for the listeners that run there. It sends to the webhooks, and calls the API with platform access tokens
// signed with the certificate in the vault. Aspire runs it with the Functions Core Tools, and gives it an Azurite emulator as
// host storage (AzureWebJobsStorage).
//
// The Core Tools add the values in local.settings.json as environment variables, unless one with the same name is set. The
// settings below use the same names, so local.settings.json's at22 vault and Service Bus without port do not end up beside them.
if (outboundInFunctions || validationInFunctions)
{
    var functionsStorage = builder.AddAzureStorage("functions-storage").RunAsEmulator();

    builder.AddAzureFunctionsProject("functions", $"{Root}/src/Events.Functions/Altinn.Platform.Events.Functions.csproj")
        .WithHostStorage(functionsStorage)
        .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
        .WithEnvironment("Platform:ApiEventsEndpoint", "http://localhost:5080/events/api/v1/")
        .WithEnvironment("KeyVault:KeyVaultURI", keyVaultUrl)
        .WithEnvironment("IDENTITY_ENDPOINT", identityEndpoint)
        .WithEnvironment("IDENTITY_HEADER", "lowkey-vault")
        .WithEnvironment("WolverineSettings:ServiceBusConnectionString", serviceBus)
        .WithEnvironment("WolverineSettings:EnableOutboundListener", outboundInFunctions.ToString())
        .WithEnvironment("WolverineSettings:EnableValidationListener", validationInFunctions.ToString())
        // The legacy Storage Queue functions are not part of the flow. Functions still creates its retry queue in Queue Storage
        // at startup, so it gets the emulator's queues until that code is gone.
        .WithReference(functionsStorage.AddQueues("functions-queues"), "QueueStorage")
        .WithEnvironment("AzureWebJobs.EventsRegistration.Disabled", "true")
        .WithEnvironment("AzureWebJobs.EventsInbound.Disabled", "true")
        .WithEnvironment("AzureWebJobs.EventsOutbound.Disabled", "true")
        .WithEnvironment("AzureWebJobs.SubscriptionValidation.Disabled", "true")
        // The host logs every request to the storage emulator.
        .WithEnvironment("AzureFunctionsJobHost__logging__logLevel__Azure.Core", "Warning")
        .WaitFor(serviceBus)
        .WaitFor(keyVault)
        .WaitFor(api);
}

builder.Build().Run();

// .NET 10 does not resolve *.localhost names itself (.NET 11 will), so it depends on the operating system.
void CheckVaultHostResolves()
{
    IPAddress[] addresses;
    try
    {
        addresses = Dns.GetHostAddresses(VaultHost);
    }
    catch (SocketException)
    {
        addresses = [];
    }

    if (!addresses.Any(IPAddress.IsLoopback))
    {
        throw new InvalidOperationException(
            $"{VaultHost} must resolve to 127.0.0.1 so the API can reach the local Key Vault. Add this line to your hosts file " +
            $"(/etc/hosts, or C:\\Windows\\System32\\drivers\\etc\\hosts on Windows):{Environment.NewLine}127.0.0.1 {VaultHost}");
    }
}

// The vault emulator and the webhook receiver serve https with the ASP.NET Core development certificate, which the API trusts
// once it is trusted on the machine (dotnet dev-certs https --trust).
string ExportDevCertificate()
{
    string path = Path.Combine(localDirectory, "devcert.pfx");
    var export = Process.Start(new ProcessStartInfo("dotnet", ["dev-certs", "https", "--export-path", path, "--password", DevCertificatePassword, "--format", "Pfx"])
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    })!;
    export.WaitForExit();
    if (export.ExitCode != 0 || !File.Exists(path))
    {
        throw new InvalidOperationException($"Could not export the development certificate: {export.StandardError.ReadToEnd()}{export.StandardOutput.ReadToEnd()}");
    }

    return path;
}

// The API signs its calls to Register with <parent of its working folder>/accesstoken/accesstokencredentials.pfx, which is mounted
// in the cluster. The project runs in src/Events, so the file goes in src/accesstoken, which git ignores.
void CopyAccessTokenSigningCertificate()
{
    string folder = Path.Combine(builder.AppHostDirectory, Root, "src/accesstoken");
    Directory.CreateDirectory(folder);
    File.Copy(TestCertificate("platform-org.pfx"), Path.Combine(folder, "accesstokencredentials.pfx"), overwrite: true);
}

// The certificates the API tests use. They are test-only, so they can be in the repository and in the local vault.
string TestCertificate(string name) => Path.Combine(builder.AppHostDirectory, Root, "test/Altinn.Platform.Events.Tests", name);

async Task SeedKeyVaultAsync(ILogger logger, CancellationToken cancellationToken)
{
    bool trustWarned = false;
    using var handler = new SocketsHttpHandler
    {
        // Straight to the container, so the seeding does not depend on how the machine resolves the vault's host name.
        ConnectCallback = async (context, ct) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(IPAddress.Loopback, keyVaultPort, ct);
            return new NetworkStream(socket, ownsSocket: true);
        },
        SslOptions = new SslClientAuthenticationOptions
        {
            // The API checks the certificate. Here it is only reported, since the API's error says less about the cause.
            RemoteCertificateValidationCallback = (_, _, _, errors) =>
            {
                if (errors != SslPolicyErrors.None && !trustWarned)
                {
                    trustWarned = true;
                    logger.LogError("The development certificate is not trusted ({Errors}), so the API cannot reach the vault or the webhook receiver. Run 'dotnet dev-certs https --trust'. On Linux, see the README.", errors);
                }

                return true;
            },
        },
    };

    using var client = new HttpClient(handler) { BaseAddress = new Uri(keyVaultUrl) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "lowkey-vault");

    // The API looks up "<issuer>-access-token-public-cert" for the issuer of each platform access token.
    using var platformCertificate = X509CertificateLoader.LoadPkcs12FromFile(TestCertificate("platform-org.pfx"), null, X509KeyStorageFlags.Exportable);
    using var ttdCertificate = X509CertificateLoader.LoadPkcs12FromFile(TestCertificate("ttd-org.pfx"), null, X509KeyStorageFlags.Exportable);
    await PutSecretAsync("platform-access-token-public-cert", Convert.ToBase64String(platformCertificate.Export(X509ContentType.Cert)));
    await PutSecretAsync("ttd-access-token-public-cert", Convert.ToBase64String(ttdCertificate.Export(X509ContentType.Cert)));

    // Functions signs its calls to the API with this one (KeyVault:PlatformCertSecretId).
    const string importPassword = "import";
    var import = await client.PostAsJsonAsync(
        "certificates/platform-access-token-private-cert/import?api-version=7.4",
        new { value = Convert.ToBase64String(platformCertificate.Export(X509ContentType.Pfx, importPassword)), pwd = importPassword },
        cancellationToken);
    await EnsureSuccessAsync(import, "certificate platform-access-token-private-cert");

    logger.LogInformation("Added the platform and ttd access token certificates to {Vault}", keyVaultUrl);

    async Task PutSecretAsync(string name, string value)
    {
        var response = await client.PutAsJsonAsync($"secrets/{name}?api-version=7.4", new { value }, cancellationToken);
        await EnsureSuccessAsync(response, "secret " + name);
    }

    async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not add {what} to the vault: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }
}
