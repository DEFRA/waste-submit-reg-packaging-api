using System.Text;
using WasteSubmitRegPackagingApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders.Testing;
using NSubstitute;

namespace WasteSubmitRegPackagingApi.Test.Authentication;

/// <summary>
/// Ported from the Manage Obligations service (DEFRA/waste-obligations), plus cases for malformed
/// headers, which must fail rather than throw.
/// </summary>
public class BasicAuthenticationHandlerTests
{
    private BasicAuthenticationHandler Subject { get; }
    private IOptionsMonitor<AuthenticationSchemeOptions> OptionsMonitor { get; } =
        Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
    private AclOptions AclOptions { get; } = new();

    public BasicAuthenticationHandlerTests()
    {
        Subject = new BasicAuthenticationHandler(
            OptionsMonitor,
            Substitute.For<ILoggerFactory>(),
            new UrlTestEncoder(),
            new OptionsWrapper<AclOptions>(AclOptions)
        );

        OptionsMonitor.Get(BasicAuthenticationHandler.SchemeName).Returns(new AuthenticationSchemeOptions());
    }

    [Fact]
    public async Task WhenNoAuthorizationHeader_ShouldNoResult()
    {
        await Subject.InitializeAsync(Scheme(), new DefaultHttpContext());

        await AuthenticateAndAssertNoResult();
    }

    [Fact]
    public async Task WhenInvalidAuthorizationHeaderScheme_ShouldNoResult()
    {
        await Subject.InitializeAsync(Scheme(), ContextWithAuthorization("InvalidScheme Value"));

        await AuthenticateAndAssertNoResult();
    }

    [Fact]
    public async Task WhenEndpointAllowsAnonymous_ShouldNoResult_EvenWithBadCredentials()
    {
        var context = ContextWithAuthorization("Basic !!!notbase64");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anonymous"));
        await Subject.InitializeAsync(Scheme(), context);

        await AuthenticateAndAssertNoResult();
    }

    [Fact]
    public async Task WhenNoCredentials_ShouldFail()
    {
        await Subject.InitializeAsync(Scheme(), ContextWithAuthorization("Basic "));

        await AuthenticateAndAssertFailure();
    }

    [Theory]
    [InlineData("Basic !!!notbase64")]
    [InlineData("Basic a b c")]
    [InlineData("Basic =")]
    public async Task WhenMalformedHeader_ShouldFailWithoutThrowing(string authorization)
    {
        AddClient("client", AclOptions.ClientType.ApiKey, "secret");
        await Subject.InitializeAsync(Scheme(), ContextWithAuthorization(authorization));

        await AuthenticateAndAssertFailure();
    }

    [Theory]
    [InlineData(":secret")]
    [InlineData("username:")]
    [InlineData("username")]
    public async Task WhenInvalidCredentials_ShouldFail(string credentials)
    {
        await Subject.InitializeAsync(Scheme(), ContextWithBasic(credentials));

        await AuthenticateAndAssertFailure();
    }

    [Fact]
    public async Task WhenNoMatchingClientId_ShouldFail()
    {
        AddClient("different-client", AclOptions.ClientType.ApiKey, "secret");
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:secret"));

        await AuthenticateAndAssertFailure();
    }

    [Fact]
    public async Task WhenMatchingClientId_AndInvalidType_ShouldFail()
    {
        AddClient("client", AclOptions.ClientType.OAuth, "secret");
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:secret"));

        await AuthenticateAndAssertFailure();
    }

    [Fact]
    public async Task WhenNoMatchingClientSecret_ShouldFail()
    {
        AddClient("client", AclOptions.ClientType.ApiKey, "different-secret");
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:secret"));

        await AuthenticateAndAssertFailure();
    }

    [Fact]
    public async Task WhenSchemeNameIsLowerCase_ShouldAuthenticate()
    {
        AddClient("client", AclOptions.ClientType.ApiKey, "secret");
        await Subject.InitializeAsync(
            Scheme(),
            ContextWithAuthorization($"basic {Convert.ToBase64String("client:secret"u8.ToArray())}"));

        var result = await Subject.AuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task WhenClientHasNoSecretConfigured_ShouldFail()
    {
        AclOptions.Clients.Add("client", new AclOptions.Client { Type = AclOptions.ClientType.ApiKey, Secret = null, Scopes = [] });
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:secret"));

        await AuthenticateAndAssertFailure();
    }

    [Fact]
    public async Task WhenSecretContainsColon_ShouldAuthenticate()
    {
        AddClient("client", AclOptions.ClientType.ApiKey, "sec:ret");
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:sec:ret"));

        var result = await Subject.AuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task WhenMatchingClientIdAndSecret_ShouldMapClientIdAndScopeClaims()
    {
        const string clientId = "client";
        AddClient(clientId, AclOptions.ClientType.ApiKey, "secret", "scope1", "scope2");
        await Subject.InitializeAsync(Scheme(), ContextWithBasic("client:secret"));

        var result = await Subject.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Contains(result.Principal.Claims, x => x is { Type: Claims.ClientId, Value: clientId });
        Assert.Contains(result.Principal.Claims, x => x is { Type: Claims.Scope, Value: "scope1" });
        Assert.Contains(result.Principal.Claims, x => x is { Type: Claims.Scope, Value: "scope2" });
    }

    private void AddClient(string clientId, AclOptions.ClientType type, string secret, params string[] scopes) =>
        AclOptions.Clients.Add(clientId, new AclOptions.Client { Type = type, Secret = secret, Scopes = scopes });

    private static DefaultHttpContext ContextWithAuthorization(string authorization) =>
        new() { Request = { Headers = { Authorization = authorization } } };

    private static DefaultHttpContext ContextWithBasic(string credentials) =>
        ContextWithAuthorization($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))}");

    private static AuthenticationScheme Scheme() =>
        new(BasicAuthenticationHandler.SchemeName, BasicAuthenticationHandler.SchemeName, typeof(BasicAuthenticationHandler));

    private async Task AuthenticateAndAssertFailure()
    {
        var result = await Subject.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        Assert.IsType<AuthenticationFailureException>(result.Failure);
        Assert.False(result.Succeeded);
    }

    private async Task AuthenticateAndAssertNoResult()
    {
        var result = await Subject.AuthenticateAsync();

        Assert.Null(result.Failure);
        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }
}
