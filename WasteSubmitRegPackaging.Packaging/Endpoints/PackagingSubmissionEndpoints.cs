using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WasteSubmitRegPackaging.Packaging.Application.Commands.CreatePackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Application.Queries.GetPackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;

namespace WasteSubmitRegPackaging.Packaging.Endpoints;

/// <summary>Public, because the validation generator skips internal request types.</summary>
public sealed class CreatePackagingSubmissionRequest
{
    [Required]
    public Guid? OrganisationId { get; init; }

    [Required, Range(2024, 2100)]
    public int? PackagingYear { get; init; }
}

internal sealed record CreatePackagingSubmissionResponse(Guid PackagingSubmissionId);

/// <summary>
/// <b>SAMPLE endpoints.</b> They show the structure (a command, a query, the user headers and the Result mapping) and
/// will be replaced as the packaging workflow is developed. They are marked "(sample)" and left out of the v1 OpenAPI
/// document. The Manage Obligations endpoints under <c>/packaging/{packagingYear}</c> are real and aren't marked.
/// </summary>
internal static class PackagingSubmissionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // A separate route and tag from the existing /packaging/{year} obligations endpoints, which still
        // live in the host and should move into this module later.
        var group = app.MapGroup("/packaging/submissions").AsSample("Packaging submissions", "packaging submission");

        group.MapPost("/", CreatePackagingSubmission)
            .WithName("CreatePackagingSubmission")
            .WithSummary("Start a packaging data submission (sample)")
            .RequireProducer();

        group.MapGet("/{packagingSubmissionId:guid}", GetPackagingSubmission)
            .WithName("GetPackagingSubmission")
            .WithSummary("A packaging data submission (sample)");
    }

    private static async Task<Results<Created<CreatePackagingSubmissionResponse>, ProblemHttpResult>> CreatePackagingSubmission(
        CreatePackagingSubmissionRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<CreatePackagingSubmissionCommand, PackagingSubmissionId> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreatePackagingSubmissionCommand(currentUser.Producer!, request.OrganisationId!.Value, request.PackagingYear!.Value),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created($"/packaging/submissions/{result.Value}", new CreatePackagingSubmissionResponse(result.Value.Value))
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> GetPackagingSubmission(
        Guid packagingSubmissionId,
        [FromServices] IQueryHandler<GetPackagingSubmissionQuery, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetPackagingSubmissionQuery(new PackagingSubmissionId(packagingSubmissionId)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
