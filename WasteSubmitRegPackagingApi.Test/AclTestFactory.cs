using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteSubmitRegPackagingApi.Test;

/// <summary>
/// Boots the real application with a known access control list, independent of
/// appsettings.Development.json, so the auth tests say exactly which clients exist.
/// </summary>
public static class AclTestFactory
{
    public const string ClientId = "TestClient";
    public const string Secret = "test-secret";

    /// <summary>Authenticates, has the read scope, but is not allowed to call Describe.</summary>
    public const string NoEndpointsClientId = "NoEndpointsClient";

    /// <summary>Authenticates and is allowed Describe, but has no read scope.</summary>
    public const string NoScopeClientId = "NoScopeClient";

    public static WebApplicationFactory<Program> Create(WebApplicationFactory<Program>? factory = null) =>
        (factory ?? new WebApplicationFactory<Program>()).WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"Acl:Clients:{ClientId}:Type", "ApiKey");
            builder.UseSetting($"Acl:Clients:{ClientId}:Secret", Secret);
            builder.UseSetting($"Acl:Clients:{ClientId}:Scopes:0", "read");
            builder.UseSetting($"Acl:Clients:{ClientId}:AllowedEndpoints:0", "Describe");

            builder.UseSetting($"Acl:Clients:{NoEndpointsClientId}:Type", "ApiKey");
            builder.UseSetting($"Acl:Clients:{NoEndpointsClientId}:Secret", Secret);
            builder.UseSetting($"Acl:Clients:{NoEndpointsClientId}:Scopes:0", "read");

            builder.UseSetting($"Acl:Clients:{NoScopeClientId}:Type", "ApiKey");
            builder.UseSetting($"Acl:Clients:{NoScopeClientId}:Secret", Secret);
            builder.UseSetting($"Acl:Clients:{NoScopeClientId}:AllowedEndpoints:0", "Describe");
        });

    public static AuthenticationHeaderValue Basic(string clientId, string secret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
}
