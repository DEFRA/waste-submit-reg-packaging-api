using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WasteSubmitRegPackaging.Fees.Application.Queries.CalculatePackagingResubmissionFee;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;

namespace WasteSubmitRegPackaging.Fees.Endpoints;

/// <summary>
/// Fee endpoints for frontends and other services. There are no user headers: a fee is the same for everyone,
/// so the <c>read</c> scope is enough.
///
/// PENCILLED IN: the request takes the fee inputs directly for now. The intended shape is a
/// PackagingSubmissionId (or, later, a RegistrationSubmissionId), with the inputs read from that submission
/// through the workflow module's contract. See docs/project_architecture.md, "Fees in a workflow".
/// </summary>
internal static class FeesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/fees").WithTags("Fees");

        // A POST because it takes several inputs, but it changes nothing.
        group.MapPost("/packaging-resubmission", CalculatePackagingResubmissionFee)
            .WithName("CalculatePackagingResubmissionFee")
            .WithSummary("Work out a packaging resubmission fee (inputs pencilled in)")
            .WithDescription(
                "Gives the same results as epr-payment-service's producer and compliance scheme resubmission-fee endpoints. " +
                "Amounts are in pence. The request will change to take a packaging submission id.")
            .ProducesValidationProblem();
    }

    private static async Task<Results<Ok<ResubmissionFee>, ProblemHttpResult>> CalculatePackagingResubmissionFee(
        CalculatePackagingResubmissionFeeRequest request,
        [FromServices] IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee> handler,
        CancellationToken cancellationToken)
    {
        // The Required checks have already run, so the ! operators are safe.
        var input = new PackagingResubmissionFeeInput(
            request.Submitter!.Value,
            request.Regulator!.Value,
            request.ResubmissionDate!.Value,
            request.MemberCount ?? 0,
            request.PreviousPayments ?? 0);

        var result = await handler.HandleAsync(new CalculatePackagingResubmissionFeeQuery(input), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}

public sealed class CalculatePackagingResubmissionFeeRequest
{
    [Required]
    public Submitter? Submitter { get; init; }

    [Required]
    public RegulatorNation? Regulator { get; init; }

    /// <summary>Matched to a rate by its UTC date. Future dates are allowed, as today.</summary>
    [Required]
    public DateTimeOffset? ResubmissionDate { get; init; }

    /// <summary>Compliance schemes only: at least 1. Ignored for a producer.</summary>
    [Range(0, 100_000)]
    public int? MemberCount { get; init; }

    /// <summary>
    /// What has already been paid, in pence. Optional for now: payments are still in epr-payment-service, so how
    /// this is found is an open question on the ticket.
    /// </summary>
    [Range(0, 1_000_000_000)]
    public decimal? PreviousPayments { get; init; }
}
