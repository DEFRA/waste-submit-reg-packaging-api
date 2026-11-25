using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace WasteSubmitRegPackagingApi.Test.Support;

/// <summary>
/// A throwaway Postgres with the real Liquibase changelog applied, shared by the tests in one class
/// (<c>IClassFixture&lt;ChangelogDatabase&gt;</c>). It needs Docker; without it, <see cref="SkipReason"/> is set
/// and the tests skip, as in the Dockerfile build. In CI the tests fail instead (<see cref="TestEnvironment.DockerIsRequired"/>).
/// </summary>
public sealed class ChangelogDatabase : IAsyncLifetime
{
    private const string LiquibaseImage = "liquibase/liquibase:5.0.1";

    private INetwork? _network;
    private PostgreSqlContainer? _postgres;

    public string? SkipReason { get; private set; }

    /// <summary>Where the database is, for a test that starts the host against it.</summary>
    public NpgsqlConnectionStringBuilder ConnectionDetails => new(_postgres!.GetConnectionString());

    public DbContextOptions<TContext> Options<TContext>(Action<DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(_postgres!.GetConnectionString())
            .UseSnakeCaseNamingConvention();
        configure?.Invoke(builder);
        return builder.Options;
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _network = new NetworkBuilder().Build();
            await _network.CreateAsync();

            _postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithNetwork(_network)
                .WithNetworkAliases("postgres")
                .Build();
            await _postgres.StartAsync();

            await ApplyChangelogAsync();
        }
        catch (DockerUnavailableException) when (!TestEnvironment.DockerIsRequired)
        {
            SkipReason = "Docker is not available, so there is no Postgres to apply the changelog to.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        if (_network is not null)
            await _network.DisposeAsync();
    }

    private async Task ApplyChangelogAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString());
        var changelog = Path.Combine(AppContext.BaseDirectory, "changelog");

        await using var liquibase = new ContainerBuilder(LiquibaseImage)
            .WithNetwork(_network)
            .WithResourceMapping(new DirectoryInfo(changelog), "/liquibase/changelog/")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand(
                "liquibase lpm add postgresql --global && " +
                "liquibase --search-path=/liquibase/changelog --changelog-file=db.changelog.xml " +
                $"--url=jdbc:postgresql://postgres:5432/{connection.Database} " +
                $"--username={connection.Username} --password={connection.Password} update")
            .Build();

        await liquibase.StartAsync();
        var exitCode = await liquibase.GetExitCodeAsync();
        if (exitCode != 0)
        {
            var (stdout, stderr) = await liquibase.GetLogsAsync();
            throw new InvalidOperationException($"Liquibase update failed ({exitCode}).\n{stdout}\n{stderr}");
        }
    }
}
