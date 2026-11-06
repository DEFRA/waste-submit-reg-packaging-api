using WasteSubmitRegPackagingApi.Authentication;
using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Test.Authentication;

public class AclOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(AclOptions.Client client) =>
        new AclOptionsValidator().Validate(null, new AclOptions { Clients = { ["Client"] = client } });

    [Fact]
    public void A_complete_api_key_client_is_valid()
    {
        var result = Validate(new AclOptions.Client { Type = AclOptions.ClientType.ApiKey, Secret = "s", Scopes = ["read"] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_empty_acl_is_valid()
    {
        Assert.True(new AclOptionsValidator().Validate(null, new AclOptions()).Succeeded);
    }

    [Fact]
    public void A_client_without_a_type_is_invalid()
    {
        var result = Validate(new AclOptions.Client { Type = null, Secret = "s", Scopes = ["read"] });

        Assert.True(result.Failed);
        Assert.Contains("Acl:Clients:Client:Type is required.", result.Failures!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_api_key_client_without_a_secret_is_invalid(string? secret)
    {
        var result = Validate(new AclOptions.Client { Type = AclOptions.ClientType.ApiKey, Secret = secret, Scopes = ["read"] });

        Assert.True(result.Failed);
        Assert.Contains("Acl:Clients:Client:Secret is required for an ApiKey client.", result.Failures!);
    }

    [Fact]
    public void An_oauth_client_is_invalid_because_only_api_keys_are_supported()
    {
        var result = Validate(new AclOptions.Client { Type = AclOptions.ClientType.OAuth, Scopes = ["read"] });

        Assert.True(result.Failed);
        Assert.Contains(
            "Acl:Clients:Client:Type 'OAuth' is not supported. This service only accepts ApiKey clients.",
            result.Failures!);
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("reader")]
    public void A_client_with_an_unknown_scope_is_invalid(string scope)
    {
        var result = Validate(new AclOptions.Client { Type = AclOptions.ClientType.ApiKey, Secret = "s", Scopes = ["read", scope] });

        Assert.True(result.Failed);
        Assert.Contains($"Acl:Clients:Client:Scopes '{scope}' is not a known scope (admin, read, write).", result.Failures!);
    }

    [Fact]
    public void A_client_without_scopes_is_invalid()
    {
        var result = Validate(new AclOptions.Client { Type = AclOptions.ClientType.ApiKey, Secret = "s", Scopes = [] });

        Assert.True(result.Failed);
        Assert.Contains("Acl:Clients:Client:Scopes must contain at least one scope.", result.Failures!);
    }

    [Fact]
    public async Task The_app_does_not_start_with_a_half_configured_client()
    {
        await using var factory = new AclTestFactory(new Dictionary<string, string?>
        {
            ["Acl:Clients:HalfConfigured:Secret"] = "secret",
            ["Acl:Clients:HalfConfigured:Scopes:0"] = "read",
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Acl:Clients:HalfConfigured:Type is required.", exception.Message);
    }
}
