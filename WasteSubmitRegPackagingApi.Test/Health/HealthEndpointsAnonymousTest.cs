using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackagingApi.Test.Utils.Postgres;

namespace WasteSubmitRegPackagingApi.Test.Health;

/// <summary>
/// CDP polls the health checks to decide whether the container is alive. They must never require
/// authentication, whatever the caller sends. If any of these fail, CDP will treat the service as
/// unhealthy and stop routing to it.
/// </summary>
public class HealthEndpointsAnonymousTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] HealthRoutes = ["/health", "/health/db"];

    private static readonly string?[] AuthorizationHeaders =
    [
        null,
        "Basic !!!notbase64",
        "Basic",
        "not a valid header",
        AclTestFactory.Basic(AclTestFactory.ClientId, "wrong-secret").ToString(),
        AclTestFactory.Basic("UnknownClient", AclTestFactory.Secret).ToString(),
        "Bearer junk",
        AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret).ToString(),
    ];

    public static TheoryData<string?> Headers() => new(AuthorizationHeaders);

    [Theory]
    [MemberData(nameof(Headers))]
    public async Task Container_health_check_is_ok_whatever_authorization_is_sent(string? authorization)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(UnreachablePostgres());
        using var client = ClientSending(factory, authorization);

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [MemberData(nameof(Headers))]
    public async Task Database_health_check_runs_whatever_authorization_is_sent(string? authorization)
    {
        // With no database the check still has to run and report Unhealthy (503), rather than being
        // blocked by authentication (401/403) or hidden (404).
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(UnreachablePostgres());
        using var client = ClientSending(factory, authorization);

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [MemberData(nameof(Headers))]
    public async Task Database_health_check_is_ok_whatever_authorization_is_sent_when_postgres_is_reachable(
        string? authorization)
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
        using var client = ClientSending(factory, authorization);

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Every_health_endpoint_is_explicitly_anonymous_and_has_no_authorization_requirement()
    {
        await using var factory = AppWithPostgres(UnreachablePostgres());
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("health", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.Equal(
            HealthRoutes.Order(),
            endpoints.Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/')).Order());

        Assert.All(endpoints, endpoint =>
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
        });
    }

    [Fact]
    public async Task There_is_no_fallback_authorization_policy()
    {
        // A fallback policy would apply to every endpoint without its own, including any health
        // check someone forgets to mark anonymous. Endpoints opt in to authentication instead.
        await using var factory = AppWithPostgres(UnreachablePostgres());
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.Null(await policyProvider.GetFallbackPolicyAsync());
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
        AclTestFactory.Create().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting($"Postgres:{key}", value);
            }
        });

    private static HttpClient ClientSending(WebApplicationFactory<Program> factory, string? authorization)
    {
        var client = factory.CreateClient();
        if (authorization is not null)
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        return client;
    }
}
