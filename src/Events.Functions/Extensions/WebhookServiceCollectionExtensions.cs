using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Functions.Extensions;

/// <summary>
/// Extension methods for registering the http client used to push events to subscriber webhooks.
/// </summary>
public static class WebhookServiceCollectionExtensions
{
    /// <summary>
    /// Registers the named webhook http client. When <see cref="EventsOutboundSettings.EnableWebhookAuthentication"/>
    /// is set, requests are authenticated with a Maskinporten token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The configuration collection.</param>
    public static IServiceCollection AddWebhookHttpClient(this IServiceCollection services, IConfiguration config)
    {
        var eventsOutboundSettings = config.GetSection("EventsOutboundSettings").Get<EventsOutboundSettings>()
            ?? new EventsOutboundSettings();

        if (eventsOutboundSettings.EnableWebhookAuthentication)
        {
            services.AddMaskinportenHttpClient<SettingsJwkClientDefinition>(AsbWebhookService.HttpClientName, config.GetSection("EventsWebhookClient:MaskinportenSettings"));
        }
        else
        {
            services.AddHttpClient(AsbWebhookService.HttpClientName);
        }

        return services;
    }
}
