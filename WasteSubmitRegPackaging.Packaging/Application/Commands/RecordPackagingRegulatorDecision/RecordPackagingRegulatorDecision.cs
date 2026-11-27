using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingRegulatorDecision;

internal sealed record RecordPackagingRegulatorDecisionCommand(
    RegulatorUser Regulator, PackagingSubmissionId PackagingSubmissionId, PackagingRegulatorDecision Decision, string? Comments)
    : ICommand<PackagingSubmissionView>;

/// <summary>
/// The regulator accepts or rejects what was submitted, which closes the cycle. After Accepted, the producer's next
/// upload is a resubmission; after Rejected it is not.
/// </summary>
internal sealed class RecordPackagingRegulatorDecisionHandler(
    IPackagingSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RecordPackagingRegulatorDecisionCommand, PackagingSubmissionView>
{
    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        RecordPackagingRegulatorDecisionCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetAsync(command.PackagingSubmissionId, cancellationToken);
        if (submission is null)
            return PackagingErrors.NotFound(command.PackagingSubmissionId);

        // Still to add: check the regulator's nation matches the submitter's (403).

        var decided = submission.RecordRegulatorDecision(
            command.Decision, command.Regulator, command.Comments, timeProvider.GetUtcNow());
        if (decided.IsFailure)
            return decided.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? PackagingSubmissionView.From(submission) : saved.Error;
    }
}
