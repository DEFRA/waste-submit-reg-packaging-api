using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.SubmitPackagingSubmission;

/// <param name="FileId">The file the producer checked.</param>
/// <param name="SubmittedBy">The name given on the declaration.</param>
internal sealed record SubmitPackagingSubmissionCommand(
    ProducerUser Producer, PackagingSubmissionId PackagingSubmissionId, Guid FileId, string SubmittedBy)
    : ICommand<PackagingSubmissionView>;

/// <summary>Submits the current cycle's file to the regulator. The same command for a first submission and a resubmission.</summary>
internal sealed class SubmitPackagingSubmissionHandler(IPackagingSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<SubmitPackagingSubmissionCommand, PackagingSubmissionView>
{
    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        SubmitPackagingSubmissionCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetAsync(command.PackagingSubmissionId, cancellationToken);
        if (submission is null)
            return PackagingErrors.NotFound(command.PackagingSubmissionId);

        if (!command.Producer.ActsFor(submission.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        var submitted = submission.SubmitToRegulator(
            command.Producer, command.FileId, command.SubmittedBy, timeProvider.GetUtcNow());
        if (submitted.IsFailure)
            return submitted.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? PackagingSubmissionView.From(submission) : saved.Error;
    }
}
