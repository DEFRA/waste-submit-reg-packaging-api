using Npgsql;

namespace WasteSubmitRegPackagingApi.Test.Support;

/// <summary>The host's <c>Postgres</c> settings for a test, to pass to <see cref="AclTestFactory"/>.</summary>
internal static class PostgresSettings
{
    public static IReadOnlyDictionary<string, string?> For(NpgsqlConnectionStringBuilder database) => new Dictionary<string, string?>
    {
        ["Postgres:Host"] = database.Host,
        ["Postgres:Port"] = database.Port.ToString(),
        ["Postgres:Database"] = database.Database,
        ["Postgres:Username"] = database.Username,
        ["Postgres:Password"] = database.Password,
        ["Postgres:UseIamAuthentication"] = "false",
        ["Postgres:SslMode"] = "Disable"
    };

    /// <summary>Nothing listens on port 1, so the connection is refused straight away.</summary>
    public static IReadOnlyDictionary<string, string?> Unreachable { get; } = new Dictionary<string, string?>
    {
        ["Postgres:Host"] = "127.0.0.1",
        ["Postgres:Port"] = "1",
        ["Postgres:UseIamAuthentication"] = "false",
        ["Postgres:SslMode"] = "Disable"
    };
}
