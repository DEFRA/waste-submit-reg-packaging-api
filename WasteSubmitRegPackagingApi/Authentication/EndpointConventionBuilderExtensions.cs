namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Marks an endpoint as secured through <see cref="EndpointConventionBuilderExtensions.RequireServiceAuthorization{TBuilder}"/>,
/// so tests can check no endpoint requires authorization without also applying the AllowedEndpoints filter.
/// </summary>
public sealed class ServiceAuthorizationMetadata;

public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// Secures an endpoint for service-to-service callers: the caller must be authenticated, have the
    /// policy's scope, and have the endpoint's name in its <see cref="AclOptions.Client.AllowedEndpoints"/>.
    /// Always use this rather than <c>RequireAuthorization</c> on its own, which would skip the
    /// AllowedEndpoints check.
    /// </summary>
    public static TBuilder RequireServiceAuthorization<TBuilder>(this TBuilder builder, string policyName)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .RequireAuthorization(policyName)
            .AddEndpointFilter<TBuilder, AllowedEndpointFilter>()
            .WithMetadata(new ServiceAuthorizationMetadata());
}
