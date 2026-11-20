using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Functions.Extensions;

/// <summary>
/// Extension methods for registering the http client used to push events to subscriber webhooks.
/// </summary>
public static class WebhookServiceCollectionExtensions
{
    private const string _maskinportenJwkSecretName = "EventsWebhookClient--MaskinportenSettings--EncodedJwk";

    /// <summary>
    /// Registers the named webhook http client. When <see cref="EventsOutboundSettings.EnableWebhookAuthentication"/>
    /// is set, requests are authenticated with a Maskinporten token using a JWK read from Key Vault. If the JWK
    /// cannot be read, a warning is written to the console and the client is registered without authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The configuration collection.</param>
    /// <param name="keyVaultService">Optional key vault service used to read the JWK.</param>
    public static IServiceCollection AddWebhookHttpClient(this IServiceCollection services, IConfiguration config, IKeyVaultService? keyVaultService = null)
    {
        var eventsOutboundSettings = config.GetSection("EventsOutboundSettings").Get<EventsOutboundSettings>()
            ?? new EventsOutboundSettings();

        MaskinportenSettings? maskinportenSettings = eventsOutboundSettings.EnableWebhookAuthentication
            ? TryGetMaskinportenSettings(config, keyVaultService ?? new KeyVaultService())
            : null;

        if (maskinportenSettings is not null)
        {
            services.AddMaskinportenHttpClient<SettingsJwkClientDefinition>(AsbWebhookService.HttpClientName, maskinportenSettings);
        }
        else
        {
            services.AddHttpClient(AsbWebhookService.HttpClientName);
        }

        return services;
    }

    private static MaskinportenSettings? TryGetMaskinportenSettings(IConfiguration config, IKeyVaultService keyVaultService)
    {
        try
        {
            string? vaultUri = config["KeyVault:KeyVaultURI"];
            if (string.IsNullOrEmpty(vaultUri))
            {
                throw new InvalidOperationException("KeyVaultURI is not configured");
            }

            MaskinportenSettings settings = config.GetSection("EventsWebhookClient:MaskinportenSettings").Get<MaskinportenSettings>()
                ?? new MaskinportenSettings();
            settings.EncodedJwk = keyVaultService.GetSecretAsync(vaultUri, _maskinportenJwkSecretName).GetAwaiter().GetResult();
            return settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read Maskinporten settings from Key Vault", ex);
        }
    }
}
