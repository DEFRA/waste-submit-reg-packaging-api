using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace WasteSubmitRegPackagingApi.Obligations.Endpoints;

public static class ObligationsEndpoints
{
    public static RouteGroupBuilder MapObligationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/packaging/{packagingYear:int}")
            .WithTags("Packaging");

        // Both return approved submissions only; a status filter can be added later if needed.
        group.MapGet("/submitter-organisation/{submitterOrganisationId:guid}/producer-material-totals", GetProducerMaterialTotals)
            .WithName("GetProducerMaterialTotals")
            .WithSummary("Material totals for each producer one submitter reported for")
            .WithDescription(
                "The producers one submitter reported for in the year: a direct registrant returns itself, " +
                "a compliance scheme returns one item per member. Returns empty items when the submitter has no approved submission " +
                "for the year, and 404 when the submitter organisation does not exist.");

        group.MapGet("/producer-material-totals-by-submitter", GetProducerMaterialTotalsBySubmitter)
            .WithName("GetProducerMaterialTotalsBySubmitter")
            .WithSummary("Material totals for each producer, grouped by submitter")
            .WithDescription(
                "Every approved submission for the year, grouped by submitter, with each producer's material totals. Not paged. " +
                "Returns empty items when the year has no approved submissions.");

        return group;
    }

    private static async Task<Results<Ok<ItemsResponse<ProducerPackaging>>, NotFound<ProblemDetails>>> GetProducerMaterialTotals(
        [FromRoute, Description("The packaging year, for example 2024.")] int packagingYear,
        [FromRoute, Description("The submitter: a direct registrant's organisation ID, or a compliance scheme's ID.")] Guid submitterOrganisationId,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        var response = await approvedSubmissionsProvider.GetProducerMaterialTotalsAsync(packagingYear, submitterOrganisationId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Submitter organisation not found",
                Detail = $"No submitter organisation with ID {submitterOrganisationId}",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Ok<ItemsResponse<ApprovedSubmission>>> GetProducerMaterialTotalsBySubmitter(
        [FromRoute, Description("The packaging year, for example 2024.")] int packagingYear,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await approvedSubmissionsProvider.GetProducerMaterialTotalsBySubmitterAsync(packagingYear, cancellationToken));
    }
}