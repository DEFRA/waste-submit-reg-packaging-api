using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriodsForSubmitter;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetRegistrationSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Endpoints;

/// <summary>
/// Read-only endpoints for frontends and other services. There are no user headers: the periods are the same
/// for everyone, so the <c>read</c> scope is enough. A frontend says who is submitting in the query string.
/// </summary>
internal static class SubmissionPeriodsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/submission-periods").WithTags("Submission periods");

        group.MapGet("/registration", GetRegistrationSubmissionPeriods)
            .WithName("GetRegistrationSubmissionPeriods")
            .WithSummary("The registration submission periods, one per window type and registration year")
            .WithDescription("The same rows and ids as epr-payment-service's [Lookup].[SubmissionPeriod]. Dates are midnight UTC.");

        group.MapGet("/packaging", GetPackagingSubmissionPeriods)
            .WithName("GetPackagingSubmissionPeriods")
            .WithSummary("Every packaging submission period, each with the data periods it accepts")
            .WithDescription("For services. Replaces the SubmissionPeriods config in the packaging frontend and the POM validation function. Times are UK local time.");

        group.MapGet("/packaging/for-submitter-type", GetPackagingSubmissionPeriodsForSubmitter)
            .WithName("GetPackagingSubmissionPeriodsForSubmitterType")
            .WithSummary("The packaging submission periods a direct producer or compliance scheme can see now, as tiles")
            .WithDescription(
                "Newest year first. Each period lists the data periods this submitter can report (H1, H2, P0 ...). " +
                "A compliance scheme gets every size; a direct producer gets only its size when producerSize is given. " +
                "Times are UK local time. Dates and codes, not sentences, so the frontend can render English and Welsh.")
            .ProducesValidationProblem();

        group.MapGet("/packaging/{submissionPeriod}/for-submitter-type", GetPackagingSubmissionPeriodForSubmitter)
            .WithName("GetPackagingSubmissionPeriodForSubmitterType")
            .WithSummary("One packaging submission period as a submitter sees it, for the pages after a tile is chosen")
            .WithDescription(
                "The submission period is its name, for example 'January to June 2026'. " +
                "404 if it doesn't exist, isn't visible yet, or has no data periods for this submitter.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<IReadOnlyList<RegistrationSubmissionPeriod>>, ProblemHttpResult>> GetRegistrationSubmissionPeriods(
        [FromQuery] int? registrationYear,
        [FromServices] IQueryHandler<GetRegistrationSubmissionPeriodsQuery, IReadOnlyList<RegistrationSubmissionPeriod>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRegistrationSubmissionPeriodsQuery(registrationYear), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<IReadOnlyList<PackagingSubmissionPeriod>>, ProblemHttpResult>> GetPackagingSubmissionPeriods(
        [FromQuery] string? periodCode,
        [FromServices] IQueryHandler<GetPackagingSubmissionPeriodsQuery, IReadOnlyList<PackagingSubmissionPeriod>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPackagingSubmissionPeriodsQuery(periodCode), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<IReadOnlyList<PackagingSubmissionPeriodTile>>, ProblemHttpResult>> GetPackagingSubmissionPeriodsForSubmitter(
        [AsParameters] GetPackagingPeriodsForSubmitterRequest request,
        [FromServices] IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>> handler,
        CancellationToken cancellationToken)
    {
        // The Required check has already run, so the ! operator is safe.
        var result = await handler.HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(request.SubmitterType!.Value, request.ProducerSize), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PackagingSubmissionPeriodTile>, ProblemHttpResult>> GetPackagingSubmissionPeriodForSubmitter(
        string submissionPeriod,
        [AsParameters] GetPackagingPeriodsForSubmitterRequest request,
        [FromServices] IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>> handler,
        CancellationToken cancellationToken)
    {
        // The handler returns a not-found error rather than an empty list when one submission period is asked for.
        var result = await handler.HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(request.SubmitterType!.Value, request.ProducerSize, submissionPeriod),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.Single()) : result.Error.ToProblem();
    }
}

/// <summary>Who is asking, from the query string: <c>?submitterType=DirectProducer&amp;producerSize=Small</c>.</summary>
public sealed class GetPackagingPeriodsForSubmitterRequest
{
    // Named, so the OpenAPI document lists them camelCase like every other query parameter.
    [Required]
    [FromQuery(Name = "submitterType")]
    public SubmitterType? SubmitterType { get; init; }

    /// <summary>A direct producer's size, if known. Leave it out for a compliance scheme.</summary>
    [FromQuery(Name = "producerSize")]
    public ProducerSize? ProducerSize { get; init; }
}
