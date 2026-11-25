using System.Net;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Platform;

/// <summary>
/// The database health check against a real Postgres. What it does when Postgres can't be reached is in
/// HealthEndpointsAnonymousTest, which needs no Docker.
/// </summary>
public class PostgresHealthCheckTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Database_health_check_is_healthy_when_postgres_is_reachable()
    {
        Assert.SkipUnless(postgres.IsAvailable, "Docker is not available, so there is no Postgres to connect to.");

        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory(PostgresSettings.For(postgres.ConnectionDetails));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
