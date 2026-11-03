using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Restricts an authenticated client to the endpoints named in its ACL entry
/// (<see cref="AclOptions.Client.AllowedEndpoints"/>, matched against the endpoint's <c>WithName</c>).
/// An endpoint not in the list returns 404.
///
/// Endpoint filters run after authentication and authorization, so this only applies to a caller
/// that is authenticated and has the endpoint's scope. Others get 401 or 403 first.
///
/// Copied from the Manage Obligations service (DEFRA/waste-obligations).
/// </summary>
public class AllowedEndpointFilter(IOptions<AclOptions> aclOptions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var endpointName = context
            .HttpContext.GetEndpoint()
            ?.Metadata.GetMetadata<IEndpointNameMetadata>()
            ?.EndpointName;

        if (string.IsNullOrWhiteSpace(endpointName))
            return Results.NotFound();

        var clientId =
            context.HttpContext.User.FindFirstValue(Claims.ClientId) ?? context.HttpContext.User.Identity?.Name;

        if (
            clientId is null
            || !aclOptions.Value.Clients.TryGetValue(clientId, out var client)
            || !client.AllowedEndpoints.Contains(endpointName, StringComparer.Ordinal)
        )
            return Results.NotFound();

        return await next(context);
    }
}
