using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Platform;

/// <summary>
/// The app refuses to start with settings it can't run on, so a bad deployment fails at once instead of on the
/// first request. The rules for an ACL client are in AclOptionsValidatorTests; these show the checks run at startup.
/// </summary>
public class StartupValidationTest
{
    [Fact]
    public void The_app_does_not_start_with_a_half_configured_client()
    {
        var exception = StartupFailure(new()
        {
            ["Acl:Clients:HalfConfigured:Secret"] = "secret",
            ["Acl:Clients:HalfConfigured:Scopes:0"] = "read",
        });

        Assert.Contains("Acl:Clients:HalfConfigured:Type is required.", exception.Message);
    }

    [Fact]
    public void The_app_does_not_start_with_an_oauth_client_because_only_api_keys_are_supported()
    {
        var exception = StartupFailure(new()
        {
            ["Acl:Clients:OAuthClient:Type"] = "OAuth",
            ["Acl:Clients:OAuthClient:Secret"] = "secret",
            ["Acl:Clients:OAuthClient:Scopes:0"] = "read",
        });

        Assert.Contains(
            "Acl:Clients:OAuthClient:Type 'OAuth' is not supported. This service only accepts ApiKey clients.",
            exception.Message);
    }

    [Fact]
    public void The_app_does_not_start_without_a_postgres_host()
    {
        var exception = StartupFailure(new() { ["Postgres:Host"] = "" });

        Assert.Contains("Host", exception.Message);
    }

    [Theory]
    [InlineData("ConnectTimeoutSeconds")]
    [InlineData("CommandTimeoutSeconds")]
    public void The_app_does_not_start_with_a_zero_postgres_timeout(string setting)
    {
        var exception = StartupFailure(new() { [$"Postgres:{setting}"] = "0" });

        Assert.Contains(setting, exception.Message);
    }

    private static OptionsValidationException StartupFailure(Dictionary<string, string?> settings)
    {
        using var factory = new AclTestFactory(settings);
        return Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
