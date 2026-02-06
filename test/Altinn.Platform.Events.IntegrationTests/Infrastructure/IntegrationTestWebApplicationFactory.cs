#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace Altinn.Platform.Events.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for integration tests that uses the real Program.cs setup
/// with test-specific overrides via in-memory configuration.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture) : WebApplicationFactory<Program>
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private IHost _host = null!;
    private readonly List<Action<IServiceCollection>> _configureTestServices = [];

    public WolverineSettings WolverineSettings { get; private set; } = null!;

    /// <summary>
    /// Gets the IHost instance for use with Wolverine's testing API.
    /// Access this after calling CreateClient() to use Wolverine's testing extensions.
    /// </summary>
    public IHost Host => _host ?? throw new InvalidOperationException("Host not created yet. Call CreateClient() or CreateDefaultClient() first.");

    /// <summary>
    /// Configures additional test services. Use this to replace services with mocks.
    /// Must be called before CreateClient().
    /// </summary>
    public IntegrationTestWebApplicationFactory ConfigureTestServices(Action<IServiceCollection> configure)
    {
        _configureTestServices.Add(configure);
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            // Load base test settings
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.integrationtest.json", optional: false, reloadOnChange: false);

            // Use in-memory configuration overrides instead of process-wide environment variables
            // This allows safe parallel test execution without race conditions
            var testConfigOverrides = new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = _fixture.ServiceBusConnectionString,
                ["PostgreSQLSettings:ConnectionString"] = _fixture.PostgresConnectionString,
                ["PostgreSQLSettings:AdminConnectionString"] = _fixture.PostgresConnectionString,
                ["PostgreSQLSettings:WorkspacePath"] = FindMigrationPath()
            };
            config.AddInMemoryCollection(testConfigOverrides);
        });

        builder.ConfigureServices((context, services) =>
        {
            // Load Wolverine settings after configuration is built
            WolverineSettings = context.Configuration.GetSection("WolverineSettings").Get<WolverineSettings>()
                ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

            Console.WriteLine($"[Factory] Loaded WolverineSettings - EnableServiceBus: {WolverineSettings.EnableServiceBus}");
            Console.WriteLine($"[Factory] ServiceBus connection: {WolverineSettings.ServiceBusConnectionString?[..50]}...");
            Console.WriteLine($"[Factory] Postgres connection: {_fixture.PostgresConnectionString[..50]}...");

            // Replace NpgsqlDataSource with test connection
            services.Replace(ServiceDescriptor.Singleton(sp =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_fixture.PostgresConnectionString);
                dataSourceBuilder.EnableParameterLogging(true);
                dataSourceBuilder.EnableDynamicJson();
                return dataSourceBuilder.Build();
            }));

            // Replace auth-related services with mocks (not needed in tests)
            services.Replace(ServiceDescriptor.Singleton(new Mock<IAuthorization>().Object));
            services.Replace(ServiceDescriptor.Singleton(new Mock<IPDP>().Object));
            services.Replace(ServiceDescriptor.Singleton(new Mock<IPublicSigningKeyProvider>().Object));
            services.Replace(ServiceDescriptor.Singleton(new Mock<IPostConfigureOptions<JwtCookieOptions>>().Object));
            services.Replace(ServiceDescriptor.Singleton(CreateEventsQueueClientMock()));

            // Apply any additional test service configuration
            foreach (var configure in _configureTestServices)
            {
                configure(services);
            }
        });

        builder.UseEnvironment("Development");

        _host = base.CreateHost(builder);
        return _host;
    }

    private static IEventsQueueClient CreateEventsQueueClientMock()
    {
        var mock = new Mock<IEventsQueueClient>();
        var successReceipt = new Models.QueuePostReceipt { Success = true };

        mock.Setup(x => x.EnqueueRegistration(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueInbound(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueOutbound(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueSubscriptionValidation(It.IsAny<string>())).ReturnsAsync(successReceipt);

        return mock.Object;
    }

    private static string FindMigrationPath()
    {
        // Navigate up from the test bin directory to find the src/Events/Migration folder
        string? currentDir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
            if (currentDir == null)
            {
                break;
            }

            string migrationPath = Path.Combine(currentDir, "src", "Events", "Migration");
            if (Directory.Exists(migrationPath))
            {
                return migrationPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Migration directory. Expected structure: <repo-root>/src/Events/Migration. " +
            $"Searched up to 10 parent directories from: {AppContext.BaseDirectory}");
    }
}
