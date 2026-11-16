using System.Collections.Generic;
using System.Linq;

using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.Platform.Events.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;

public class WebhookServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void AddWebhookHttpClient_RegistersMaskinportenOnlyWhenAuthenticationEnabled(string flag, bool expectMaskinporten)
    {
        // Arrange
        Dictionary<string, string> settings = new()
        {
            ["EventsOutboundSettings:MaskinportenSettings:Scope"] = "altinn:system/events.event.push"
        };

        if (flag != null)
        {
            settings["EventsOutboundSettings:EnableWebhookAuthentication"] = flag;
        }

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();

        // Act
        services.AddWebhookHttpClient(config);

        // Assert
        Assert.Equal(expectMaskinporten, services.Any(s => s.ServiceType == typeof(IMaskinportenService)));
    }
}
