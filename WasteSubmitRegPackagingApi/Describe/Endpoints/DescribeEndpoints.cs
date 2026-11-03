using WasteSubmitRegPackagingApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace WasteSubmitRegPackagingApi.Describe.Endpoints;

/// <summary>
/// A no-op endpoint that says which service answered. It exists so callers (the packaging submit
/// frontend first) can prove service-to-service authentication end to end without touching data.
///
/// It requires authentication, unlike <c>/health</c>, which CDP polls and must stay anonymous.
/// </summary>
public static class DescribeEndpoints
{
    public const string ServiceName = "waste-submit-reg-packaging-api";
    public const string EndpointName = "Describe";

    public static RouteHandlerBuilder MapDescribeEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/describe", Describe)
            .WithName(EndpointName)
            .WithTags("Service")
            .WithSummary("Name of this service. Requires service-to-service authentication.")
            .RequireAuthorization(PolicyNames.Read)
            .AddEndpointFilter<AllowedEndpointFilter>();

    private static Ok<DescribeResponse> Describe() => TypedResults.Ok(new DescribeResponse(ServiceName));
}

public sealed record DescribeResponse(string Service);
