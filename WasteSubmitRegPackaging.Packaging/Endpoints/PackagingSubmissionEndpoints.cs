using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WasteSubmitRegPackaging.Packaging.Application;
using WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingFileScanResult;
using WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingRegulatorDecision;
using WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingResubmissionFeePayment;
using WasteSubmitRegPackaging.Packaging.Application.Commands.StartPackagingFileUpload;
using WasteSubmitRegPackaging.Packaging.Application.Commands.SubmitPackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Application.Commands.ValidatePackagingFile;
using WasteSubmitRegPackaging.Packaging.Application.Queries.GetPackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Endpoints;

/// <summary>
/// <b>SAMPLE endpoints</b> for the packaging data workflow: the whole flow, thin (docs/packaging_workflow.md). They are
/// marked "(sample)" and left out of the v1 OpenAPI document. The Manage Obligations endpoints under
/// <c>/packaging/{packagingYear}</c> are real and aren't marked.
///
/// Every producer and regulator endpoint returns the same view, with the status and the next step.
/// </summary>
internal static class PackagingSubmissionEndpoints
{
    private const string Area = "packaging submission";

    public static void Map(IEndpointRouteBuilder app)
    {
        var producer = app.MapGroup("/packaging/submissions").AsSample("Packaging submissions", Area);

        producer.MapGet("/", FindPackagingSubmission)
            .WithName("FindPackagingSubmission")
            .WithSummary("A submitter's packaging submission for a submission period, or 404 if not started (sample)")
            .RequireAuthorization(ModulePolicies.Read)
            .RequireProducer();

        producer.MapGet("/{packagingSubmissionId:guid}", GetPackagingSubmission)
            .WithName("GetPackagingSubmission")
            .WithSummary("A packaging submission, with its status and next step (sample)")
            .RequireAuthorization(ModulePolicies.Read)
            .RequireProducer();

        producer.MapPost("/file-uploads", StartPackagingFileUpload)
            .WithName("StartPackagingFileUpload")
            .WithSummary("Start a file upload. The first one creates the packaging submission (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireProducer();

        producer.MapPost("/{packagingSubmissionId:guid}/resubmission-fee-payment", RecordPackagingResubmissionFeePayment)
            .WithName("RecordPackagingResubmissionFeePayment")
            .WithSummary("Resubmissions only: record the payment reference for the fee (sample, placeholder)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireProducer();

        producer.MapPost("/{packagingSubmissionId:guid}/submit", SubmitPackagingSubmission)
            .WithName("SubmitPackagingSubmission")
            .WithSummary("Submit the checked file to the regulator (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireProducer();

        // The CDP uploader's callback. It is the one endpoint with no API key and no user: the uploader sends neither,
        // so the secret token in the URL is what protects it. EndpointConventionsTest lists it by name.
        app.MapGroup("/packaging/file-uploads").AsSample("Packaging submissions", Area)
            .MapPost("/{fileId:guid}/scan-result/{token}", RecordPackagingFileScanResult)
            .WithName("RecordPackagingFileScanResult")
            .WithSummary("The CDP uploader's callback: a file was stored or rejected (sample)")
            .AllowAnonymous();

        app.MapGroup("/regulator/packaging/submissions").AsSample("Regulator", Area)
            .MapPost("/{packagingSubmissionId:guid}/decision", RecordPackagingRegulatorDecision)
            .WithName("RecordPackagingRegulatorDecision")
            .WithSummary("Accept or reject a packaging submission (sample)")
            .RequireAuthorization(ModulePolicies.Write)
            .RequireRegulator();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> FindPackagingSubmission(
        [FromQuery, Required] Guid organisationId,
        [FromQuery] Guid? complianceSchemeId,
        [FromQuery, Required] string submissionPeriod,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IQueryHandler<FindPackagingSubmissionQuery, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new FindPackagingSubmissionQuery(currentUser.Producer!, organisationId, complianceSchemeId, submissionPeriod),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> GetPackagingSubmission(
        Guid packagingSubmissionId,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IQueryHandler<GetPackagingSubmissionQuery, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetPackagingSubmissionQuery(new PackagingSubmissionId(packagingSubmissionId), currentUser.Producer!),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<StartPackagingFileUploadResponse>, ProblemHttpResult>> StartPackagingFileUpload(
        StartPackagingFileUploadRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<StartPackagingFileUploadCommand, StartPackagingFileUploadResult> handler,
        CancellationToken cancellationToken)
    {
        // The Required checks have already run, so the ! operators are safe.
        var result = await handler.HandleAsync(
            new StartPackagingFileUploadCommand(
                currentUser.Producer!, request.OrganisationId!.Value, request.ComplianceSchemeId, request.SubmissionPeriod!),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new StartPackagingFileUploadResponse(
                result.Value.PackagingSubmissionId,
                result.Value.FileId,
                $"/packaging/file-uploads/{result.Value.FileId}/scan-result/{result.Value.CallbackToken}"))
            : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RecordPackagingFileScanResult(
        Guid fileId,
        string token,
        PackagingFileScanResultRequest request,
        [FromServices] ICommandHandler<RecordPackagingFileScanResultCommand, Unit> handler,
        [FromServices] ICommandHandler<ValidatePackagingFileCommand, Unit> validate,
        CancellationToken cancellationToken)
    {
        var file = request.Form!.File!;

        // "complete" with a place in S3 is a stored file. Anything else was rejected and not stored.
        var stored = file is { FileStatus: "complete", Filename: not null, S3Bucket: not null, S3Key: not null }
            ? new StoredFile(file.Filename, file.S3Bucket, file.S3Key)
            : null;

        var result = await handler.HandleAsync(
            new RecordPackagingFileScanResultCommand(fileId, token, file.Filename, stored, file.ErrorMessage),
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // PLACEHOLDER trigger for validation. The file is stored and saved as Validating; validating it is a separate
        // command, started here only because the placeholder validator is instant. When validation is real, delete
        // this line and have a background worker call the same command. Nothing else changes. Its result isn't the
        // uploader's concern: a file that is no longer Validating (a repeated callback) is simply left alone.
        if (stored is not null)
            await validate.HandleAsync(new ValidatePackagingFileCommand(fileId), cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> RecordPackagingResubmissionFeePayment(
        Guid packagingSubmissionId,
        RecordPackagingResubmissionFeePaymentRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<RecordPackagingResubmissionFeePaymentCommand, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RecordPackagingResubmissionFeePaymentCommand(
                currentUser.Producer!,
                new PackagingSubmissionId(packagingSubmissionId),
                request.PaymentReference!),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> SubmitPackagingSubmission(
        Guid packagingSubmissionId,
        SubmitPackagingSubmissionRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<SubmitPackagingSubmissionCommand, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new SubmitPackagingSubmissionCommand(
                currentUser.Producer!, new PackagingSubmissionId(packagingSubmissionId), request.FileId!.Value, request.SubmittedBy!),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PackagingSubmissionView>, ProblemHttpResult>> RecordPackagingRegulatorDecision(
        Guid packagingSubmissionId,
        RecordPackagingRegulatorDecisionRequest request,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICommandHandler<RecordPackagingRegulatorDecisionCommand, PackagingSubmissionView> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RecordPackagingRegulatorDecisionCommand(
                currentUser.Regulator!, new PackagingSubmissionId(packagingSubmissionId), request.Decision!.Value, request.Comments),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
