using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace WasteSubmitRegPackagingApi.Obligations.Endpoints;

public static class ObligationsEndpoints
{
    public static RouteGroupBuilder MapObligationsEndpoints(this IEndpointRouteBuilder app)
    {
        // The path is TBC on the ticket; agree it with the Obligations team before this goes live.
        var group = app.MapGroup("/obligations")
            .WithTags("Obligations");

        group.MapGet("/organisations/{organisationId:guid}/approved-submissions", GetApprovedSubmissions)
            .WithName("GetApprovedSubmissions");

        return group;
    }

    private static async Task<Results<Ok<ItemsResponse<OrganisationPackaging>>, NotFound<ProblemDetails>>> GetApprovedSubmissions(
        [FromRoute] Guid organisationId,
        [FromQuery] int packagingYear,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken,
        [FromQuery] bool aggregate = true)
    {
        var response = await approvedSubmissionsProvider.GetApprovedSubmissionsAsync(
            organisationId, packagingYear, aggregate, cancellationToken);

        if (response is null)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Approved submissions not found",
                Detail = $"No approved submissions for organisation {organisationId} in {packagingYear}",
                Status = StatusCodes.Status404NotFound
            });
        }

        return TypedResults.Ok(response);
    }
}