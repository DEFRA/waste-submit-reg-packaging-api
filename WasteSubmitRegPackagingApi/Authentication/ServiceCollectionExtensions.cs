using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Service-to-service authentication and authorisation, following the Manage Obligations service
/// (DEFRA/waste-obligations). Only the API key (Basic) scheme and the <c>read</c> scope exist: add
/// more (for example an OAuth client type, or write/admin scopes) when an endpoint needs them.
///
/// There is deliberately no fallback policy. Endpoints opt in with <c>RequireAuthorization</c>, and
/// the CDP health checks must stay anonymous.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationAuthorization(this IServiceCollection services)
    {
        services.AddOptions<AclOptions>().BindConfiguration("Acl").ValidateOnStart();
        services.AddSingleton<IValidateOptions<AclOptions>, AclOptionsValidator>();
        services.AddSingleton<IValidateOptions<AclOptions>, AclClientTypeValidator>();

        services
            .AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                BasicAuthenticationHandler.SchemeName,
                _ => { }
            );

        services
            .AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.Read, builder => RequireScope(builder, Scopes.Read));

        return services;
    }

    private static void RequireScope(AuthorizationPolicyBuilder builder, string scope) =>
        builder
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName)
            .RequireClaim(Claims.Scope, scope);
}
