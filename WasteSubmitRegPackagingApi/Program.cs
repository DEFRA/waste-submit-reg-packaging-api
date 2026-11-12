using WasteSubmitRegPackagingApi.Authentication;
using WasteSubmitRegPackaging.Packaging;
using WasteSubmitRegPackaging.Registration;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;
using WasteSubmitRegPackagingApi.Describe.Endpoints;
using WasteSubmitRegPackagingApi.Obligations.Endpoints;
using WasteSubmitRegPackagingApi.Obligations.Services;
using WasteSubmitRegPackagingApi.Config;
using WasteSubmitRegPackagingApi.Utils;
using WasteSubmitRegPackagingApi.Utils.Http;
using WasteSubmitRegPackagingApi.Utils.Postgres;
using System.Diagnostics.CodeAnalysis;
using WasteSubmitRegPackagingApi.Utils.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
    ConfigurePostgres(services, configuration);
    ConfigureModules(services, configuration);
    ConfigureCommonDataApi(services, configuration);
    ConfigureOpenApi(services);

    services.AddHealthChecks()
        .AddPostgres();

    // Service-to-service authentication (API key ACL), copied from Manage Obligations. Endpoints opt
    // in; there is no fallback policy, so the CDP health checks stay anonymous.
    services.AddAuthenticationAuthorization();
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
static void ConfigureCommonDataApi(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<CommonDataApiConfig>()
        .Bind(configuration.GetRequiredSection("CommonDataApi"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services
        .AddHttpClientWithTracingAndProxy<IApprovedSubmissionsProvider, CommonDataApiApprovedSubmissionsProvider>()
        .ConfigureHttpClient((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<CommonDataApiConfig>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/api/submissions/");
        });
}

[ExcludeFromCodeCoverage]
static void ConfigureModules(IServiceCollection services, IConfiguration configuration)
{
    // Each business area is a module with its own project, tables (reg_*, pkg_*) and endpoints.
    // The host only wires them up. See docs/architecture.md.
    services.AddSharedKernel(configuration);
    services.AddRegistrationModule();
    services.AddPackagingModule();
}

[ExcludeFromCodeCoverage]
static void ConfigureOpenApi(IServiceCollection services)
{
    services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Waste Submit Reg Packaging API";
        document.Info.Description =
            "Packaging and registration data for EPR. The obligations endpoints serve aggregated packaging data from " +
            "approved submissions. The Registration, Regulator and Packaging submissions endpoints are skeleton samples.";
        return Task.CompletedTask;
    }));
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    app.UseSerilogRequestLogging();

    app.UseHeaderPropagation();

    app.UseAuthentication();
    app.UseAuthorization();
}

[ExcludeFromCodeCoverage]
static void ConfigureEndpoints(WebApplication app)
{
    // CDP polls /health to decide whether the container is alive, so it leaves out the database.
    // Health checks must never require authentication; HealthEndpointsAnonymousTest guards this.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains(PostgresHealthChecks.Tag)
    }).AllowAnonymous();

    app.MapHealthChecks("/health/db", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(PostgresHealthChecks.Tag)
    }).AllowAnonymous();

    // Off in production. Swagger:Enabled turns it on per environment.
    if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.MapOpenApi();

        // Relative, so the page still finds the document behind CDP's /{service-name} path prefix.
        app.UseSwaggerUI(options => options.SwaggerEndpoint("../openapi/v1.json", "Waste Submit Reg Packaging API"));
    }

    app.MapObligationsEndpoints();

    app.MapDescribeEndpoints();

    app.MapRegistrationEndpoints();
    app.MapPackagingEndpoints();
}