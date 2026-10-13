using Npgsql;

namespace WasteSubmitRegPackagingApi.Utils.Postgres;

public static class PostgresHealthChecks
{
    /// <summary>
    /// Checks with this tag are served from /health/db and left out of /health, because CDP restarts
    /// the container whenever /health fails and a database blip should not do that.
    /// </summary>
    public const string Tag = "db";

    // Aurora Serverless can take 2 to 9 seconds to answer the first query after a quiet spell.
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(15);

    public static IHealthChecksBuilder AddPostgres(this IHealthChecksBuilder builder) =>
        builder.AddNpgSql(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>(),
            name: "postgres",
            tags: [Tag],
            timeout: s_timeout);
}
