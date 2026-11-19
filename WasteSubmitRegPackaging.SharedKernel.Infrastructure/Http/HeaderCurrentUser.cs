using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

/// <summary>
/// The headers a calling frontend sets to say which of its users it is acting for.
///
/// PLACEHOLDER, to discuss: the caller is already authenticated service-to-service (the ACL), so these
/// headers are trusted only because only allow-listed services can reach this API. Alternatives are a
/// user token passed through from Defra ID, or a signed user token.
/// </summary>
public static class UserHeaders
{
    public const string UserId = "X-User-Id";
    public const string Type = "X-User-Type";
    public const string OrganisationId = "X-Organisation-Id";
}

internal sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ProducerUser? Producer =>
        Read() is (var userId, UserType.Producer, { } organisationId) ? new ProducerUser(userId, organisationId) : null;

    public RegulatorUser? Regulator =>
        Read() is (var userId, UserType.Regulator, _) ? new RegulatorUser(userId) : null;

    public UserType? Type => Read()?.Type;

    private (Guid UserId, UserType Type, Guid? OrganisationId)? Read()
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null)
            return null;

        if (!Guid.TryParse(headers[UserHeaders.UserId], out var userId))
            return null;

        if (!Enum.TryParse<UserType>(headers[UserHeaders.Type], ignoreCase: true, out var type) || !Enum.IsDefined(type))
            return null;

        Guid? organisationId = Guid.TryParse(headers[UserHeaders.OrganisationId], out var parsed) ? parsed : null;

        return (userId, type, organisationId);
    }
}

/// <summary>
/// Rejects a request that isn't from the kind of user the endpoint is for, before it reaches the endpoint:
/// 400 <c>user.missing</c> when the headers are missing or invalid, 403 <c>user.producers_only</c> or
/// <c>user.regulators_only</c> when they name the other kind of user.
/// </summary>
internal sealed class RequireUserFilter(UserType required) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Resolved per request: filters are created once per endpoint, and the current user is scoped.
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        var found = required switch
        {
            UserType.Producer => currentUser.Producer is not null,
            UserType.Regulator => currentUser.Regulator is not null,
            _ => false
        };

        if (found)
            return await next(context);

        if (currentUser.Type is { } other && other != required)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: $"Only a {required.ToString().ToLowerInvariant()} user can do this",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = required == UserType.Producer ? "user.producers_only" : "user.regulators_only"
                });
        }

        var expected = required == UserType.Producer
            ? $"{UserHeaders.UserId} (a GUID), {UserHeaders.Type}: Producer, and {UserHeaders.OrganisationId} (a GUID)"
            : $"{UserHeaders.UserId} (a GUID) and {UserHeaders.Type}: Regulator";

        return TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Missing or invalid user",
            detail: $"Send {expected}.",
            extensions: new Dictionary<string, object?> { ["code"] = "user.missing" });
    }
}

/// <summary>
/// Marks an endpoint that needs the user headers, so the OpenAPI document (and Swagger UI) can list them.
/// </summary>
public sealed record RequiresUserMetadata(UserType UserType);

public static class UserEndpointExtensions
{
    /// <summary>For endpoints a producer user calls. The endpoint can then use <see cref="ICurrentUser.Producer"/>.</summary>
    public static TBuilder RequireProducer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequireUser(UserType.Producer);

    /// <summary>For endpoints a regulator user calls. The endpoint can then use <see cref="ICurrentUser.Regulator"/>.</summary>
    public static TBuilder RequireRegulator<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequireUser(UserType.Regulator);

    private static TBuilder RequireUser<TBuilder>(this TBuilder builder, UserType userType)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .AddEndpointFilter(new RequireUserFilter(userType))
            .WithMetadata(new RequiresUserMetadata(userType));
}
