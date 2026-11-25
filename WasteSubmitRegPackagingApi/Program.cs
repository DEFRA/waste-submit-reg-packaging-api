using WasteSubmitRegPackagingApi.Authentication;
using WasteSubmitRegPackaging.Packaging;
using WasteSubmitRegPackaging.Fees;
using WasteSubmitRegPackaging.SubmissionPeriods;
using WasteSubmitRegPackaging.Registration;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Modules;
using WasteSubmitRegPackagingApi.Describe.Endpoints;
using WasteSubmitRegPackagingApi.Obligations.Endpoints;
using WasteSubmitRegPackagingApi.Obligations.Services;
using WasteSubmitRegPackagingApi.Config;
using WasteSubmitRegPackagingApi.Utils;
using WasteSubmitRegPackagingApi.Utils.ErrorHandling;
using WasteSubmitRegPackagingApi.Utils.Http;
using WasteSubmitRegPackagingApi.Utils.Postgres;
using System.Diagnostics.CodeAnalysis;
using WasteSubmitRegPackagingApi.Utils.Logging;
using WasteSubmitRegPackagingApi.Utils.Metrics;
using WasteSubmitRegPackagingApi.Utils.OpenApi;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
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
    services.AddRequestMetrics();

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
    var postgres = services
        .AddOptions<PostgresConfig>()
        .Bind(configuration.GetRequiredSection("Postgres"))
        .ValidateDataAnnotations();

    // The build starts the app to write the OpenAPI documents, with no database settings. It never connects.
    if (!OpenApiBuild.IsRunning)
    {
        postgres.ValidateOnStart();
    }

    services.AddSingleton(serviceProvider =>
        PostgresDataSourceFactory.Create(serviceProvider.GetRequiredService<IOptions<PostgresConfig>>().Value));
}

[ExcludeFromCodeCoverage]
static void ConfigureCommonDataApi(IServiceCollection services, IConfiguration configuration)
{
    const string name = "CommonDataApi";

    services
        .AddOptions<CommonDataApiConfig>()
        .Bind(configuration.GetRequiredSection(name))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Timeouts and retries, as Manage Obligations configures its downstream clients.
    services.AddOptions<HttpStandardResilienceOptions>(name).BindConfiguration(name);

    services
        .AddHttpClientWithTracingAndProxy<IApprovedSubmissionsProvider, CommonDataApiApprovedSubmissionsProvider>()
        .ConfigureHttpClient((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<CommonDataApiConfig>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/api/submissions/");
        })
        .AddResiliencePipeline(name);
}

[ExcludeFromCodeCoverage]
static void ConfigureModules(IServiceCollection services, IConfiguration configuration)
{
    // Each business area is a module with its own project, tables (reg_*, pkg_*) and endpoints.
    // The host only wires them up. See docs/project_architecture.md.
    services.AddSharedKernel(configuration);
    services.AddRegistrationModule();
    services.AddPackagingModule();
    services.AddFeesModule();
    services.AddSubmissionPeriodsModule();
}

[ExcludeFromCodeCoverage]
static void ConfigureOpenApi(IServiceCollection services)
{
    // Two documents. "v1" is the published contract: it's merged, and frontends generate clients from it. It leaves
    // out endpoints marked .Unpublished() (samples included). "unpublished" has only those, so they can still be seen
    // and called from Swagger. The build writes them to openapi.json and openapi_unpublished.json (see the csproj).
    services.AddOpenApi(OpenApiDocuments.V1, options =>
    {
        options.ShouldInclude = description => !IsUnpublished(description);
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Waste Submit Reg Packaging API";
            document.Info.Description =
                "Packaging and registration data for EPR. The obligations endpoints serve aggregated packaging data from " +
                "approved submissions. Endpoints that aren't published yet are in the unpublished document.";
            return Task.CompletedTask;
        }).AddSwaggerAuthTransformers();
    });

    services.AddOpenApi(OpenApiDocuments.Unpublished, options =>
    {
        options.ShouldInclude = IsUnpublished;
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Waste Submit Reg Packaging API: unpublished";
            document.Info.Description =
                "Endpoints that aren't published yet, including the SAMPLE registration, regulator and packaging " +
                "submission endpoints. They may change, so don't generate clients from this document.";
            return Task.CompletedTask;
        }).AddSwaggerAuthTransformers();
    });

    static bool IsUnpublished(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription description) =>
        description.ActionDescriptor.EndpointMetadata.OfType<UnpublishedEndpointMetadata>().Any();
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    // Outside the error handler, so the request log records the status the caller gets (such as 504), not 500.
    app.UseSerilogRequestLogging();
    // Outside authentication, so rejected requests are counted with the status the caller got. Skipped when the
    // build starts the app to write the OpenAPI documents: that run has no AWS_EMF_NAMESPACE and serves no requests.
    if (!OpenApiBuild.IsRunning)
    {
        app.UseRequestMetrics();
    }

    // Problem responses for unhandled exceptions, including 504 when a downstream call times out.
    app.UseErrorHandling();

    app.UseHeaderPropagation();

    // Endpoints marked .Unpublished() are a 404 unless this is on: locally, and wherever cdp-app-config turns it on.
    // Before authentication, so callers get a 404 whether or not they send credentials.
    app.UseUnpublishedEndpointGate(app.Configuration.GetValue<bool>("UnpublishedEndpoints:Enabled"));

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
        var unpublishedEnabled = app.Configuration.GetValue<bool>("UnpublishedEndpoints:Enabled");

        // The unpublished document is a 404 wherever its endpoints are, so it never describes endpoints nobody can call.
        app.MapOpenApi().AddEndpointFilter(async (context, next) =>
            !unpublishedEnabled
            && OpenApiDocuments.Unpublished.Equals(context.HttpContext.GetRouteValue("documentName") as string, StringComparison.OrdinalIgnoreCase)
                ? Results.NotFound()
                : await next(context));

        // Relative, so the page still finds the document behind CDP's /{service-name} path prefix.
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"../openapi/{OpenApiDocuments.V1}.json", "Waste Submit Reg Packaging API");
            if (unpublishedEnabled)
            {
                options.SwaggerEndpoint($"../openapi/{OpenApiDocuments.Unpublished}.json", "Unpublished");
            }

            // Keeps the Authorize credentials when the page reloads.
            options.EnablePersistAuthorization();
        });
    }

    app.MapObligationsEndpoints();

    app.MapDescribeEndpoints();

    app.MapRegistrationEndpoints();
    app.MapPackagingEndpoints();
    app.MapFeesEndpoints();
    app.MapSubmissionPeriodsEndpoints();
}