using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace WasteSubmitRegPackagingApi.Obligations.Endpoints;

public static class ObligationsEndpoints
{
    public static RouteGroupBuilder MapObligationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/packaging/{year:int}")
            .WithTags("Packaging");

        // Both return approved submissions only; a status filter can be added later if needed.
        group.MapGet("/organisation/{organisationId:guid}/aggregated-submission", GetAggregatedSubmission)
            .WithName("GetAggregatedSubmission");

        group.MapGet("/aggregated-submissions", GetAggregatedSubmissions)
            .WithName("GetAggregatedSubmissions");

        return group;
    }

    private static async Task<Results<Ok<ItemsResponse<OrganisationPackaging>>, NotFound<ProblemDetails>>> GetAggregatedSubmission(
        [FromRoute] int year,
        [FromRoute] Guid organisationId,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        var response = await approvedSubmissionsProvider.GetAggregatedSubmissionAsync(year, organisationId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : NotFoundProblem($"No approved submissions for organisation {organisationId} in {year}");
    }

    private static async Task<Results<Ok<ApprovedSubmissionsResponse>, NotFound<ProblemDetails>>> GetAggregatedSubmissions(
        [FromRoute] int year,
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