using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Common.PEP.Authorization;
using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Implementation;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Clients;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Formatters;
using Altinn.Platform.Events.Health;
using Altinn.Platform.Events.Middleware;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Swagger;
using Altinn.Platform.Events.Telemetry;
using AltinnCore.Authentication.JwtCookie;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Security.KeyVault.Secrets;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wolverine;
using Wolverine.AzureServiceBus;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

ILogger logger;

string vaultApplicationInsightsKey = "ApplicationInsights--InstrumentationKey";

string applicationInsightsConnectionString = string.Empty;

var builder = WebApplication.CreateBuilder(args);

ConfigureWebHostCreationLogging();

await SetConfigurationProviders(builder.Configuration);

ConfigureApplicationLogging(builder.Logging);

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Configure(builder.Configuration);

app.Run();

void ConfigureWebHostCreationLogging()
{
    var logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Altinn.Platform.Events.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;

    config.SetBasePath(basePath);
    string configJsonFile1 = $"{basePath}/altinn-appsettings/altinn-dbsettings-secret.json";
    string configJsonFile2 = $"{Directory.GetCurrentDirectory()}/appsettings.json";

    if (basePath == "/")
    {
        configJsonFile2 = "/app/appsettings.json";
    }

    config.AddJsonFile(configJsonFile1, optional: true, reloadOnChange: true);

    config.AddJsonFile(configJsonFile2, optional: false, reloadOnChange: true);

    config.AddUserSecrets<Program>();

    config.AddEnvironmentVariables();

    await ConnectToKeyVaultAndSetApplicationInsights(config);

    config.AddCommandLine(args);
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);
    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        logger.LogInformation("Program // Configure key vault client // App");
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", keyVaultSettings.ClientId);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", keyVaultSettings.ClientSecret);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", keyVaultSettings.TenantId);
        var azureCredentials = new DefaultAzureCredential();

        config.AddAzureKeyVault(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        SecretClient client = new SecretClient(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        try
        {
            KeyVaultSecret keyVaultSecret = await client.GetSecretAsync(vaultApplicationInsightsKey);
            applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", keyVaultSecret.Value);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, $"Unable to read application insights key.");
        }
    }
}

void ConfigureApplicationLogging(ILoggingBuilder logging)
{
    logging.AddOpenTelemetry(builder =>
    {
       builder.IncludeFormattedMessage = true;
       builder.IncludeScopes = true; 
    });
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    logger.LogInformation("Program // ConfigureServices");

    services.AddOpenTelemetry()
        .ConfigureResource(resourceBuilder => resourceBuilder.AddService(serviceName: TelemetryClient.AppName, serviceInstanceId: Environment.MachineName))
        .WithMetrics(metrics => 
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "System.Net.Http",
                TelemetryClient.AppName);
        })
        .WithTracing(tracing => 
        {
            if (builder.Environment.IsDevelopment())
            {
                tracing.SetSampler(new AlwaysOnSampler());
            }

            tracing.AddSource(TelemetryClient.AppName);
            tracing.AddAspNetCoreInstrumentation();

            tracing.AddHttpClientInstrumentation();
            tracing.AddNpgsql();

            tracing.AddProcessor(new RequestFilterProcessor(new HttpContextAccessor()));
        });

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        AddAzureMonitorTelemetryExporters(services, applicationInsightsConnectionString);
    }

    services.AddAutoMapper(typeof(Program));
    services.AddSingleton<TelemetryClient>();

    services.AddMemoryCache();
    services.AddHealthChecks().AddCheck<HealthCheck>("events_health_check");

    if (config.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
    {
        var connectionString = string.Format(
                config.GetValue<string>("PostgreSQLSettings:ConnectionString"),
                config.GetValue<string>("PostgreSQLSettings:EventsDbPwd"));

        services.AddHealthChecks()
            .AddNpgSql(connectionString);

        services.AddNpgsqlDataSource(connectionString, builder => builder
                .EnableParameterLogging(true)
                .EnableDynamicJson()
                .ConfigureTracing(o => o
                    .ConfigureCommandSpanNameProvider(cmd => cmd.CommandText)
                    .ConfigureCommandFilter(cmd => true)
                    .EnableFirstResponseEvent(false)));
    }

    WolverineSettings wolverineSettings = config.GetSection("WolverineSettings").Get<WolverineSettings>() ?? new WolverineSettings();

    // Set static settings for handlers before Wolverine discovers them
    SaveEventHandler.Settings = wolverineSettings;
    SendToOutboundHandler.Settings = wolverineSettings;
    SendEventToSubscriberHandler.Settings = wolverineSettings;
    ValidateSubscriptionHandler.Settings = wolverineSettings;

    services.AddWolverine(opts =>
    {
        if (wolverineSettings.EnableServiceBus)
        {
            opts.ConfigureEventsDefaults(
                builder.Environment,
                wolverineSettings.ServiceBusConnectionString);

            opts.PublishMessage<RegisterEventCommand>()
                .ToAzureServiceBusQueue(wolverineSettings.RegistrationQueueName);
            opts.PublishMessage<InboundEventCommand>()
                .ToAzureServiceBusQueue(wolverineSettings.InboundQueueName);
            opts.PublishMessage<OutboundEventCommand>()
                .ToAzureServiceBusQueue(wolverineSettings.OutboundQueueName);                
            opts.PublishMessage<ValidateSubscriptionCommand>()
                .ToAzureServiceBusQueue(wolverineSettings.ValidationQueueName);

            opts.ListenToAzureServiceBusQueue(wolverineSettings.RegistrationQueueName)
                .ListenerCount(wolverineSettings.ListenerCount)
                .ProcessInline();
            opts.ListenToAzureServiceBusQueue(wolverineSettings.InboundQueueName)
                .ListenerCount(wolverineSettings.ListenerCount)
                .ProcessInline();
            opts.ListenToAzureServiceBusQueue(wolverineSettings.OutboundQueueName)
                .ListenerCount(wolverineSettings.ListenerCount)
                .ProcessInline();
            opts.ListenToAzureServiceBusQueue(wolverineSettings.ValidationQueueName)
                .ListenerCount(wolverineSettings.ListenerCount)
                .ProcessInline();
        }

        opts.Policies.AllListeners(x => x.ProcessInline());
        opts.Policies.AllSenders(x => x.SendInline());
    });

    services.AddSingleton(config);
    services.Configure<PostgreSqlSettings>(config.GetSection("PostgreSQLSettings"));
    services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
    services.Configure<QueueStorageSettings>(config.GetSection("QueueStorageSettings"));
    services.Configure<PlatformSettings>(config.GetSection("PlatformSettings"));
    services.Configure<WolverineSettings>(config.GetSection("WolverineSettings"));
    services.Configure<EventsOutboundSettings>(config.GetSection("EventsOutboundSettings"));
    services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
    services.Configure<Altinn.Common.PEP.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));

    services.AddSingleton<IAuthorizationHandler, AccessTokenHandler>();
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProvider>();
    services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
    services.AddTransient<ISigningCredentialsResolver, SigningCredentialsResolver>();

    services.AddHttpClient<AuthorizationApiClient>();

    services.AddAuthentication(JwtCookieDefaults.AuthenticationScheme)
          .AddJwtCookie(JwtCookieDefaults.AuthenticationScheme, options =>
          {
              GeneralSettings generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();
              options.JwtCookieName = generalSettings.JwtCookieName;
              options.MetadataAddress = generalSettings.OpenIdWellKnownEndpoint;
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  ValidateIssuerSigningKey = true,
                  ValidateIssuer = false,
                  ValidateAudience = false,
                  RequireExpirationTime = true,
                  ValidateLifetime = true,
                  ClockSkew = TimeSpan.Zero
              };

              if (builder.Environment.IsDevelopment())
              {
                  options.RequireHttpsMetadata = false;
              }
          });

    services.AddAuthorizationBuilder()
        .AddPolicy(AuthorizationConstants.POLICY_PUBLISH_SCOPE_OR_PLATFORM_ACCESS, policy =>
        {
            policy.Requirements.Add(new PublishScopeOrAccessTokenRequirement(AuthorizationConstants.SCOPE_EVENTS_PUBLISH));
        })
        .AddPolicy(AuthorizationConstants.POLICY_PLATFORM_ACCESS, policy => policy.Requirements.Add(new AccessTokenRequirement()))
        .AddPolicy(AuthorizationConstants.POLICY_SCOPE_EVENTS_PUBLISH, policy => policy.Requirements.Add(new ScopeAccessRequirement(AuthorizationConstants.SCOPE_EVENTS_PUBLISH)))
        .AddPolicy(AuthorizationConstants.POLICY_SCOPE_EVENTS_SUBSCRIBE, policy => policy.Requirements.Add(new ScopeAccessRequirement(AuthorizationConstants.SCOPE_EVENTS_SUBSCRIBE)));

    services.AddControllers(opts =>
    {
        opts.InputFormatters.Insert(0, new CloudEventJsonInputFormatter(new JsonEventFormatter()));
        opts.OutputFormatters.Insert(0, new CloudEventJsonOutputFormatter(new JsonEventFormatter()));
        opts.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    services.AddHttpClient<IRegisterService, RegisterService>();
    services.AddSingleton<ITraceLogService, TraceLogService>();
    services.AddScoped<IEventsService, EventsService>();
    services.AddScoped<IOutboundService, OutboundService>();
    services.AddScoped<ISubscriptionService, SubscriptionService>();
    services.AddScoped<IAppSubscriptionService, AppSubscriptionService>();
    services.AddScoped<IGenericSubscriptionService, GenericSubscriptionService>();
    services.AddSingleton<ICloudEventRepository, CloudEventRepository>();
    services.AddSingleton<ISubscriptionRepository, SubscriptionRepository>();
    services.AddSingleton<ITraceLogRepository, TraceLogRepository>();
    services.Decorate<ISubscriptionRepository, SubscriptionRepositoryCachingDecorator>();
    services.AddSingleton<IEventsQueueClient, EventsQueueClient>();
    services.AddSingleton<IPDP, PDPAppSI>();
    services.AddTransient<IAuthorizationHandler, ScopeAccessHandler>();
    services.AddHttpClient<IWebhookService, WebhookService>();

    services.AddTransient<IAuthorization, AuthorizationService>();
    services.AddTransient<IClaimsPrincipalProvider, ClaimsPrincipalProvider>();

    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Altinn Platform Events", Version = "v1" });
        IncludeXmlComments(c);
        c.EnableAnnotations();

        // Adding filters to provide object examples
        c.SchemaFilter<SchemaExampleFilter>();
        c.RequestBodyFilter<RequestBodyExampleFilter>();

        // add JWT Authentication
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter JWT token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "bearer"
        });

        c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });
}

void AddAzureMonitorTelemetryExporters(IServiceCollection services, string applicationInsightsConnectionString)
{
    services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddAzureMonitorLogExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
    services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddAzureMonitorMetricExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
    services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = applicationInsightsConnectionString;
    }));
}

void IncludeXmlComments(SwaggerGenOptions swaggerGenOptions)
{
    try
    {
        string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        swaggerGenOptions.IncludeXmlComments(xmlPath);
    }
    catch (Exception e)
    {
        logger.LogWarning(e, "Program // Exception when attempting to include the XML comments file(s).");
    }
}

void Configure(IConfiguration config)
{
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/events/api/v1/error");
    }

    if (config.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
    {
        ConsoleTraceService traceService = new ConsoleTraceService { IsDebugEnabled = true };

        string connectionString = string.Format(
            config.GetValue<string>("PostgreSQLSettings:AdminConnectionString"),
            config.GetValue<string>("PostgreSQLSettings:EventsDbAdminPwd"));

        app.UseYuniql(
            new PostgreSqlDataService(traceService),
            new PostgreSqlBulkImportService(traceService),
            traceService,
            new Yuniql.AspNetCore.Configuration
            {
                Workspace = Path.Combine(Environment.CurrentDirectory, config.GetValue<string>("PostgreSQLSettings:WorkspacePath")),
                ConnectionString = connectionString,
                IsAutoCreateDatabase = false,
                IsDebug = true
            });
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseMiddleware<EnableRequestBodyBufferingMiddleware>();

    app.MapControllers();

    app.MapHealthChecks("/health");
}

/// <summary>
/// Startup class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed partial class Program
{
    private Program()
    {
    }
}
