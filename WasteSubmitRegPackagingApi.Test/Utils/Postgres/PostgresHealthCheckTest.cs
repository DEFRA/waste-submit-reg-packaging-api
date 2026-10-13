using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Test.Utils.Postgres;

public class PostgresHealthCheckTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    // Nothing listens on port 1, so the connection is refused straight away.
    private static readonly Dictionary<string, string?> s_unreachablePostgres = new()
    {
        ["Host"] = "127.0.0.1",
        ["Port"] = "1",
        ["UseIamAuthentication"] = "false",
        ["SslMode"] = "Disable"
    };

    [Fact]
    public async Task Database_health_check_is_healthy_when_postgres_is_reachable()
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
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Database_health_check_is_unhealthy_when_postgres_is_unreachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(s_unreachablePostgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Container_health_check_ignores_the_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWithPostgres(s_unreachablePostgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task App_does_not_start_without_a_postgres_host()
    {
        await using var factory = AppWithPostgres(new Dictionary<string, string?> { ["Host"] = "" });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Host", exception.Message);
    }

    private static WebApplicationFactory<Program> AppWithPostgres(IReadOnlyDictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting($"Postgres:{key}", value);
            }
        });
}
