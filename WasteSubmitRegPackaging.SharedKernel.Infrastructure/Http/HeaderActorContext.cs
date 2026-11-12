using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using WasteSubmitRegPackaging.SharedKernel.Actors;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

/// <summary>
/// The headers a calling frontend sets to say which user it is acting for.
///
/// PLACEHOLDER, to discuss: the caller is already authenticated service-to-service (the ACL), so these
/// headers are trusted only because only allow-listed services can reach this API. Alternatives are a
/// user token passed through from Defra ID, or a signed actor token.
/// </summary>
public static class ActorHeaders
{
    public const string UserId = "X-Actor-User-Id";
    public const string OrganisationId = "X-Actor-Organisation-Id";
    public const string Type = "X-Actor-Type";
}

internal sealed class HeaderActorContext(IHttpContextAccessor httpContextAccessor) : IActorContext
{
    public Actor? Current
    {
        get
        {
            var headers = httpContextAccessor.HttpContext?.Request.Headers;
            if (headers is null)
                return null;

            if (!Guid.TryParse(headers[ActorHeaders.UserId], out var userId))
                return null;

            if (!Enum.TryParse<ActorType>(headers[ActorHeaders.Type], ignoreCase: true, out var type)
                || !Enum.IsDefined(type))
                return null;

            Guid? organisationId = Guid.TryParse(headers[ActorHeaders.OrganisationId], out var parsed) ? parsed : null;

            return new Actor(userId, organisationId, type);
        }
    }
}

/// <summary>Rejects a request with no valid actor before it reaches the endpoint.</summary>
internal sealed class RequireActorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Resolved per request: filters are created once per endpoint, and the actor context is scoped.
        var actorContext = context.HttpContext.RequestServices.GetRequiredService<IActorContext>();

        if (actorContext.Current is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing or invalid actor",
                detail: $"Send {ActorHeaders.UserId} (a GUID) and {ActorHeaders.Type} (Producer or Regulator).",
                extensions: new Dictionary<string, object?> { ["code"] = "actor.missing" });
        }

        return await next(context);
    }
}

/// <summary>
/// Marks an endpoint that needs the actor headers, so the OpenAPI document (and Swagger UI) can list them.
/// </summary>
public sealed class RequiresActorMetadata
{
    public static readonly RequiresActorMetadata Instance = new();

    private RequiresActorMetadata() { }
}

public static class ActorEndpointExtensions
{
    /// <summary>Use on every endpoint that changes state, so each recorded event has an actor.</summary>
    public static TBuilder RequireActor<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .AddEndpointFilter<TBuilder, RequireActorFilter>()
            .WithMetadata(RequiresActorMetadata.Instance);
}
