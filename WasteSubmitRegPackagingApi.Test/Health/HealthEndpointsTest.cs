using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackagingApi.Test.Utils.Postgres;

namespace WasteSubmitRegPackagingApi.Test.Health;

/// <summary>
/// CDP polls the health checks to decide whether the container is alive. If any of these fail, CDP will
/// treat the service as unhealthy and stop routing to it.
/// </summary>
public class HealthEndpointsTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] HealthRoutes = ["/health", "/health/db"];

    [Fact]
    public async Task Container_health_check_is_ok()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(UnreachablePostgres());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Database_health_check_reports_unhealthy_when_postgres_is_unreachable()
    {
        // With no database the check still has to run and report Unhealthy (503), rather than being hidden (404).
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(UnreachablePostgres());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Database_health_check_is_ok_when_postgres_is_reachable()
    {
        Assert.SkipUnless(postgres.IsAvailable, "Docker is not available, so there is no Postgres to connect to.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var details = postgres.ConnectionDetails;
        await using var factory = AppWithPostgres(new Dictionary<string, string?>
        {
            ["Host"] = details.Host,
            ["Port"] = details.Port.ToString(),
            ["Database"] = details.Database,
            ["Username"] = details.Username,
            ["Password"] = details.Password,
            ["UseIamAuthentication"] = "false",
            ["SslMode"] = "Disable"
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Both_health_endpoints_are_mapped()
    {
        await using var factory = AppWithPostgres(UnreachablePostgres());
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("health", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.Equal(
            HealthRoutes.Order(),
            endpoints.Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/')).Order());
    }

    // Nothing listens on port 1, so the connection is refused straight away.
    private static Dictionary<string, string?> UnreachablePostgres() => new()
    {
        ["Host"] = "127.0.0.1",
        ["Port"] = "1",
        ["UseIamAuthentication"] = "false",
        ["SslMode"] = "Disable"
    };

    private static WebApplicationFactory<Program> AppWithPostgres(IReadOnlyDictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting($"Postgres:{key}", value);
        });

}
