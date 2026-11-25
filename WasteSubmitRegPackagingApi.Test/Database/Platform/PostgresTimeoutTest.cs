using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Npgsql;
using WasteSubmitRegPackagingApi.Config;
using WasteSubmitRegPackagingApi.Utils.Postgres;

namespace WasteSubmitRegPackagingApi.Test.Database.Platform;

/// <summary>
/// The PRR asks for a timeout on every call out of the service, proven by tests. These prove both Postgres
/// timeouts fire at the configured value: one for a connection that never completes, one for a slow query.
/// </summary>
public class PostgresTimeoutTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void Uses_the_npgsql_defaults_of_15_seconds_to_connect_and_30_seconds_per_command()
    {
        var config = new PostgresConfig { Host = "localhost", Database = "db", Username = "user" };

        Assert.Equal(15, config.ConnectTimeoutSeconds);
        Assert.Equal(30, config.CommandTimeoutSeconds);
    }

    [Theory]
    [InlineData("ConnectTimeoutSeconds")]
    [InlineData("CommandTimeoutSeconds")]
    public void App_does_not_start_with_a_zero_postgres_timeout(string setting)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting($"Postgres:{setting}", "0"));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(setting, exception.Message);
    }

    [Fact]
    public async Task A_query_that_runs_past_the_command_timeout_is_stopped()
    {
        Assert.SkipUnless(postgres.IsAvailable, "Docker is not available, so there is no Postgres to connect to.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var details = postgres.ConnectionDetails;
        await using var dataSource = PostgresDataSourceFactory.Create(new PostgresConfig
        {
            Host = details.Host!,
            Port = details.Port,
            Database = details.Database!,
            Username = details.Username!,
            Password = details.Password,
            SslMode = SslMode.Disable,
            CommandTimeoutSeconds = 1
        });
        await using var command = dataSource.CreateCommand("SELECT pg_sleep(10)");

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<NpgsqlException>(() => command.ExecuteNonQueryAsync(cancellationToken));

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_connection_the_server_never_answers_is_abandoned_at_the_connect_timeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Accepts the TCP connection and then says nothing, like a database that has hung.
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        await using var dataSource = PostgresDataSourceFactory.Create(new PostgresConfig
        {
            Host = "127.0.0.1",
            Port = ((IPEndPoint)silentServer.LocalEndpoint).Port,
            Database = "db",
            Username = "user",
            Password = "password",
            SslMode = SslMode.Disable,
            ConnectTimeoutSeconds = 1
        });

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<NpgsqlException>(async () =>
            await dataSource.OpenConnectionAsync(cancellationToken));

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    }
}
