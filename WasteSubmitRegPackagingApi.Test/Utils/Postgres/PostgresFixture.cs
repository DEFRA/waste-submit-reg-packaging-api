using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace WasteSubmitRegPackagingApi.Test.Utils.Postgres;

/// <summary>
/// A throwaway Postgres on the same major version as CDP's Aurora clusters. The Dockerfile runs the
/// tests inside the image build, where there is no Docker, so tests that need it skip there instead of failing.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable => _container is not null;

    public NpgsqlConnectionStringBuilder ConnectionDetails =>
        new(_container?.GetConnectionString() ?? throw new InvalidOperationException("Postgres is not running"));

    public async ValueTask InitializeAsync()
    {
        try
        {
            var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await container.StartAsync();
            _container = container;
        }
        catch (DockerUnavailableException)
        {
            // No Docker, so IsAvailable stays false.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
