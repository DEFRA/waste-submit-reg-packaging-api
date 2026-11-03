using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Service-to-service authentication and authorisation, following the Manage Obligations service
/// (DEFRA/waste-obligations). Only the API key (Basic) scheme is registered: no OAuth client calls
/// this API yet.
///
/// There is deliberately no fallback policy. Endpoints opt in with <c>RequireAuthorization</c>, and
/// the CDP health checks must stay anonymous.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationAuthorization(this IServiceCollection services)
    {
        services.AddOptions<AclOptions>().BindConfiguration("Acl").ValidateDataAnnotations().ValidateOnStart();
        services.AddTransient<AllowedEndpointFilter>();

        services
            .AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                BasicAuthenticationHandler.SchemeName,
                _ => { }
            );

        services
            .AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.Admin, builder => RequireScope(builder, Scopes.Admin))
            .AddPolicy(PolicyNames.Read, builder => RequireScope(builder, Scopes.Read))
            .AddPolicy(PolicyNames.Write, builder => RequireScope(builder, Scopes.Write));

        return services;
    }

    private static void RequireScope(AuthorizationPolicyBuilder builder, string scope) =>
        builder
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName)
            .RequireClaim(Claims.Scope, scope);
}
