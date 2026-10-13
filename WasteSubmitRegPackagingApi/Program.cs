using WasteSubmitRegPackagingApi.Obligations.Endpoints;
using WasteSubmitRegPackagingApi.Obligations.Services;
using WasteSubmitRegPackagingApi.Config;
using WasteSubmitRegPackagingApi.Utils;
using WasteSubmitRegPackagingApi.Utils.Http;
using WasteSubmitRegPackagingApi.Utils.Mongo;
using WasteSubmitRegPackagingApi.Utils.Postgres;
using System.Diagnostics.CodeAnalysis;
using WasteSubmitRegPackagingApi.Utils.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;
using Microsoft.Extensions.Options;
using Serilog;

var app = BuildApp(args);
await app.RunAsync();

[ExcludeFromCodeCoverage]
static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    ConfigureHost(builder);
    ConfigureServices(builder);

    var app = builder.Build();

    ConfigureMiddleware(app);
    ConfigureEndpoints(app);

    return app;
}

[ExcludeFromCodeCoverage]
static void ConfigureHost(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog(CdpLogging.Configuration);
}

[ExcludeFromCodeCoverage]
static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;
    var configuration = builder.Configuration;

    // Trust material must be loaded before anything creates outbound connections.
    services.LoadCustomTrustStoreFromEnvironment();

    services.AddProblemDetails();
    services.AddValidation();

    services.AddHttpContextAccessor();

    ConfigureHeaderPropagation(services, configuration);
    ConfigureHttpClients(services);
    ConfigureMongo(services, configuration);
    ConfigurePostgres(services, configuration);
    ConfigureOpenApi(services);

    services.AddHealthChecks()
        .AddPostgres();

    // App services
    services.AddSingleton<IApprovedSubmissionsProvider, StubApprovedSubmissionsProvider>();
}

[ExcludeFromCodeCoverage]
static void ConfigureHeaderPropagation(IServiceCollection services, IConfiguration configuration)
{
    var traceHeader = configuration.GetValue<string>("TraceHeader");

    services.AddHeaderPropagation(options =>
    {
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });
}

[ExcludeFromCodeCoverage]
static void ConfigureHttpClients(IServiceCollection services)
{
    services.AddTransient<ProxyHttpMessageHandler>();

    // services.AddHttpClientWithTracing<IExampleClient, ExampleClient>();
    // services.AddHttpClientWithProxy<IExternalClient, ExternalClient>();
}

[ExcludeFromCodeCoverage]
static void ConfigureMongo(IServiceCollection services, IConfiguration configuration)
{

    MongoExtensions.Register();
    MongoConventions.Register();

    services
        .AddOptions<MongoConfig>()
        .Bind(configuration.GetRequiredSection("Mongo"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
}

[ExcludeFromCodeCoverage]
static void ConfigurePostgres(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<PostgresConfig>()
        .Bind(configuration.GetRequiredSection("Postgres"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton(serviceProvider =>
        PostgresDataSourceFactory.Create(serviceProvider.GetRequiredService<IOptions<PostgresConfig>>().Value));
}

[ExcludeFromCodeCoverage]
static void ConfigureOpenApi(IServiceCollection services)
{
    services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Waste Submit Reg Packaging API";
        document.Info.Description =
            "Aggregated packaging data from approved submissions, for the Obligations service. Serves stub data for now.";
        return Task.CompletedTask;
    }));
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    app.UseSerilogRequestLogging();

    app.UseHeaderPropagation();
}

[ExcludeFromCodeCoverage]
static void ConfigureEndpoints(WebApplication app)
{
    // CDP polls /health to decide whether the container is alive, so it leaves out the database.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains(PostgresHealthChecks.Tag)
    });

    app.MapHealthChecks("/health/db", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(PostgresHealthChecks.Tag)
    });

    // Off in production. Swagger:Enabled turns it on per environment.
    if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.MapOpenApi();

        // Relative, so the page still finds the document behind CDP's /{service-name} path prefix.
        app.UseSwaggerUI(options => options.SwaggerEndpoint("../openapi/v1.json", "Waste Submit Reg Packaging API"));
    }

    app.MapObligationsEndpoints();
}