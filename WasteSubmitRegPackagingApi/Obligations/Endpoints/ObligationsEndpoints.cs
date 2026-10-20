using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WasteSubmitRegPackagingApi.Obligations.Endpoints;

public static class ObligationsEndpoints
{
    private const string AggregatedFormat = "aggregated";

    public static RouteGroupBuilder MapObligationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/packaging/{year:int}")
            .WithTags("Packaging");

        // Both return approved submissions only; a status filter can be added later if needed.
        // Both take format=aggregated, the only format for now, so other formats can be added without breaking callers.
        group.MapGet("/submitter-organisation/{organisationId:guid}/packaging-submissions", GetAggregatedSubmission)
            .WithName("GetAggregatedSubmission")
            .WithSummary("Approved packaging data for one submitter")
            .WithDescription(
                "The organisations one submitter reported for in the year: a direct registrant returns itself, " +
                "a compliance scheme returns one item per member. Returns empty items when the submitter has no approved submission " +
                "for the year, 404 when the submitter organisation does not exist, and 400 unless format is aggregated.");

        group.MapGet("/packaging-submissions", GetAggregatedSubmissions)
            .WithName("GetAggregatedSubmissions")
            .WithSummary("Every approved submission for a year")
            .WithDescription(
                "Every approved submission for the year, grouped by submitter. Not paged. " +
                "Returns empty items when the year has no approved submissions, and 400 unless format is aggregated.");

        return group;
    }

    private static async Task<Results<Ok<ItemsResponse<OrganisationPackaging>>, NotFound<ProblemDetails>, ValidationProblem>> GetAggregatedSubmission(
        [FromRoute, Description("The packaging year, for example 2024.")] int year,
        [FromRoute, Description("The submitter's organisation ID: a direct registrant or a compliance scheme.")] Guid organisationId,
        [FromQuery, Required, Description("How the submissions are returned. Only aggregated for now.")] string? format,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        if (UnsupportedFormat(format) is { } formatProblem)
        {
            return formatProblem;
        }

        var response = await approvedSubmissionsProvider.GetAggregatedSubmissionAsync(year, organisationId, cancellationToken);

        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Submitter organisation not found",
                Detail = $"No submitter organisation with ID {organisationId}",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Results<Ok<ItemsResponse<ApprovedSubmission>>, ValidationProblem>> GetAggregatedSubmissions(
        [FromRoute, Description("The packaging year, for example 2024.")] int year,
        [FromQuery, Required, Description("How the submissions are returned. Only aggregated for now.")] string? format,
        [FromServices] IApprovedSubmissionsProvider approvedSubmissionsProvider,
        CancellationToken cancellationToken)
    {
        if (UnsupportedFormat(format) is { } formatProblem)
        {
            return formatProblem;
        }

        return TypedResults.Ok(await approvedSubmissionsProvider.GetAggregatedSubmissionsAsync(year, cancellationToken));
    }

    private static ValidationProblem? UnsupportedFormat(string? format) =>
        string.Equals(format, AggregatedFormat, StringComparison.OrdinalIgnoreCase)
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["format"] = [$"Use format={AggregatedFormat}. It is the only format for now."]
            });
}