namespace WasteSubmitRegPackagingApi.Config;

using System.ComponentModel.DataAnnotations;
using Npgsql;

public class PostgresConfig
{
    /// <summary>
    /// The cluster's writer endpoint. Set per environment in cdp-app-config.
    /// </summary>
    [Required]
    public required string Host { get; init; }

    [Range(1, 65535)]
    public int Port { get; init; } = 5432;

    [Required]
    public required string Database { get; init; }

    [Required]
    public required string Username { get; init; }

    /// <summary>
    /// CDP gives no password: the service signs in with a short-lived token from its IAM role.
    /// Turn this off to use <see cref="Password"/> against a local database.
    /// </summary>
    public bool UseIamAuthentication { get; init; }

    /// <summary>
    /// Local databases only. Ignored when <see cref="UseIamAuthentication"/> is on.
    /// </summary>
    public string? Password { get; init; }

    public SslMode SslMode { get; init; } = SslMode.VerifyFull;
}
