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
        var group = app.MapGroup("/packaging/{year:int}")
            .WithTags("Packaging");

        // Both return approved submissions only; a status filter can be added later if needed.
        group.MapGet("/organisation/{organisationId:guid}/aggregated-submission", GetAggregatedSubmission)
            .WithName("GetAggregatedSubmission")
            .WithSummary("Approved packaging data for one submitter")
            .WithDescription(
                "The organisations one submitter reported for in the year: a direct registrant returns itself, " +
                "a compliance scheme returns one item per member. Returns 404 when the submitter has no approved submission for the year.");

        group.MapGet("/aggregated-submissions", GetAggregatedSubmissions)
            .WithName("GetAggregatedSubmissions")
            .WithSummary("Every approved submission for a year")
            .WithDescription(
                "Every approved submission for the year, grouped by submitter. Not paged. " +
                "Returns 404 when the year has no approved submissions.");

        return group;
    }

    private static async Task<Results<Ok<ItemsResponse<OrganisationPackaging>>, NotFound<ProblemDetails>>> GetAggregatedSubmission(
        [FromRoute, Description("The packaging year, for example 2024.")] int year,
        [FromRoute, Description("The submitter's organisation ID: a direct registrant or a compliance scheme.")] Guid organisationId,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        var response = await approvedSubmissionsProvider.GetAggregatedSubmissionAsync(year, organisationId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : NotFoundProblem($"No approved submissions for organisation {organisationId} in {year}");
    }

    private static async Task<Results<Ok<ApprovedSubmissionsResponse>, NotFound<ProblemDetails>>> GetAggregatedSubmissions(
        [FromRoute, Description("The packaging year, for example 2024.")] int year,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        var response = await approvedSubmissionsProvider.GetAggregatedSubmissionsAsync(year, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : NotFoundProblem($"No approved submissions in {year}");
    }

    private static NotFound<ProblemDetails> NotFoundProblem(string detail) =>
        TypedResults.NotFound(new ProblemDetails
        {
            Title = "Approved submissions not found",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        });
}