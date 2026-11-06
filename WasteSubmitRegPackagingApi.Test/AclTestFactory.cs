using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteSubmitRegPackagingApi.Test;

/// <summary>
/// Boots the real application with known test clients added to the access control list. It runs in
/// the Development environment, so the <c>Developer</c> client from appsettings.Development.json is
/// present too; tests use only the clients defined here.
/// </summary>
public sealed class AclTestFactory(IReadOnlyDictionary<string, string?>? extraSettings = null)
    : WebApplicationFactory<Program>
{
    public const string ClientId = "TestClient";
    public const string Secret = "test-secret";

    /// <summary>Authenticates, but has write rather than the read scope.</summary>
    public const string NoReadScopeClientId = "NoReadScopeClient";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting($"Acl:Clients:{ClientId}:Type", "ApiKey");
        builder.UseSetting($"Acl:Clients:{ClientId}:Secret", Secret);
        builder.UseSetting($"Acl:Clients:{ClientId}:Scopes:0", "read");

        builder.UseSetting($"Acl:Clients:{NoReadScopeClientId}:Type", "ApiKey");
        builder.UseSetting($"Acl:Clients:{NoReadScopeClientId}:Secret", Secret);
        builder.UseSetting($"Acl:Clients:{NoReadScopeClientId}:Scopes:0", "write");

        foreach (var (key, value) in extraSettings ?? new Dictionary<string, string?>())
            builder.UseSetting(key, value);
    }

    public static AuthenticationHeaderValue Basic(string clientId, string secret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
}
