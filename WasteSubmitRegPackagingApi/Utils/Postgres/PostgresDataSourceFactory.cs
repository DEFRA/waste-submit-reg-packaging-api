using Amazon.RDS.Util;
using Npgsql;
using WasteSubmitRegPackagingApi.Config;

namespace WasteSubmitRegPackagingApi.Utils.Postgres;

public static class PostgresDataSourceFactory
{
    // RDS IAM tokens last 15 minutes, so fetch a new one well before the old one expires.
    private static readonly TimeSpan s_tokenRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan s_tokenRetryInterval = TimeSpan.FromSeconds(10);

    public static NpgsqlDataSource Create(PostgresConfig config)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host,
            Port = config.Port,
            Database = config.Database,
            Username = config.Username,
            Password = config.UseIamAuthentication ? null : config.Password,
            SslMode = config.SslMode
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        if (config.UseIamAuthentication)
        {
            // The token is signed locally with the service's IAM role, and the region comes from AWS_REGION.
            builder.UsePeriodicPasswordProvider(
                (settings, _) => ValueTask.FromResult(RDSAuthTokenGenerator.GenerateAuthToken(
                    settings.Host!, settings.Port, settings.Username!)),
                s_tokenRefreshInterval,
                s_tokenRetryInterval);
        }

        return builder.Build();
    }
}
