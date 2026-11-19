using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Altinn.Platform.Events.Tools.PlatformMocks.Configuration;
using Altinn.Platform.Events.Tools.PlatformMocks.Mocks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WireMock.Logging;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace Altinn.Platform.Events.Tools.PlatformMocks;

/// <summary>
/// Starts one WireMock server for each enabled system, and logs every request and response.
/// </summary>
internal sealed class PlatformMocksService(IOptions<PlatformMocksSettings> options, ILogger<PlatformMocksService> logger) : IHostedService
{
    internal static readonly ActivitySource ActivitySource = new("Altinn.Platform.Events.Tools.PlatformMocks");

    private readonly PlatformMocksSettings _settings = options.Value;
    private readonly Dictionary<string, WireMockServer> _servers = [];

    /// <summary>
    /// The URL of each running server, by system name.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Urls => _servers.ToDictionary(s => s.Key, s => s.Value.Url!);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Start(_settings.Register, new RegisterMock(_settings.Register));
        Start(_settings.Authorization, new AuthorizationMock(_settings.Authorization));
        Start(_settings.Webhooks, new WebhooksMock(_settings.Webhooks.SlowSeconds), _settings.Webhooks);

        if (_servers.Count == 0)
        {
            logger.LogWarning("All platform mocks are disabled");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var server in _servers.Values)
        {
            server.Stop();
            server.Dispose();
        }

        _servers.Clear();
        return Task.CompletedTask;
    }

    private void Start(MockServerSettings serverSettings, IPlatformMock mock, WebhooksMockSettings? https = null)
    {
        if (!serverSettings.Enabled)
        {
            logger.LogInformation("{System} mock is disabled", mock.System);
            return;
        }

        var wireMockSettings = new WireMockServerSettings
        {
            Urls = [$"{(https is null ? "http" : "https")}://localhost:{serverSettings.Port}"],
            StartAdminInterface = false,
            MaxRequestLogCount = 100,
        };

        if (https is not null)
        {
            if (string.IsNullOrEmpty(https.CertificatePath))
            {
                throw new InvalidOperationException($"The {mock.System} mock serves https and needs PlatformMocksSettings:Webhooks:CertificatePath.");
            }

            wireMockSettings.UseSSL = true;
            wireMockSettings.CertificateSettings = new WireMockCertificateSettings
            {
                X509CertificateFilePath = https.CertificatePath,
                X509CertificatePassword = https.CertificatePassword,
            };
        }

        var server = WireMockServer.Start(wireMockSettings);
        mock.AddMappings(server);

        server.LogEntriesChanged += (_, e) =>
        {
            foreach (ILogEntry entry in e.NewItems ?? Array.Empty<ILogEntry>())
            {
                LogEntry(mock.System, entry);
            }
        };

        _servers[mock.System] = server;
        logger.LogInformation("{System} mock listening on {Url}", mock.System, server.Url);
    }

    private void LogEntry(string system, ILogEntry entry)
    {
        var request = entry.RequestMessage;
        var response = entry.ResponseMessage;
        if (request is null || response is null)
        {
            return;
        }

        // Continue the caller's trace, so the entry shows up under the API's span in the dashboard.
        ActivityContext.TryParse(Header(request.Headers, "traceparent"), Header(request.Headers, "tracestate"), out var parent);
        using var activity = ActivitySource.StartActivity($"{system} {request.Method} {request.Path}", ActivityKind.Server, parent, startTime: request.DateTime);
        activity?.SetTag("http.response.status_code", response.StatusCode);

        // Only test data passes through the mocks, so the payloads are logged as they are.
        logger.LogInformation(
            "{System} {Method} {Path} -> {StatusCode}\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            system,
            request.Method,
            request.Path,
            response.StatusCode,
            BodyText(request.BodyData),
            BodyText(response.BodyData));
    }

    // WireMock keeps a body as a string, parsed JSON or bytes, depending on what it detected.
    private static string? BodyText(IBodyData? body)
    {
        return body?.BodyAsString
            ?? (body?.BodyAsJson is { } json ? JsonSerializer.Serialize(json) : null)
            ?? (body?.BodyAsBytes is { } bytes ? Encoding.UTF8.GetString(bytes) : null);
    }

    private static string? Header(IDictionary<string, WireMockList<string>>? headers, string name)
    {
        return headers is not null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }
}
