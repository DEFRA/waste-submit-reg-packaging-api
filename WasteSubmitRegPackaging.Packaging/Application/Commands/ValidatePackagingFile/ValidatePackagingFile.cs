using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.ValidatePackagingFile;

internal sealed record ValidatePackagingFileCommand(Guid FileId) : ICommand<Unit>;

/// <summary>
/// Validates a stored file and records the result. This is the only thing that makes a file Valid or Invalid.
///
/// Validation is a step of its own, after the upload and apart from it: the file waits as <c>Validating</c> until
/// this runs, and callers re-read the submission until its next step moves on. So it can take as long as it needs,
/// and whatever runs it (the callback endpoint today, a background worker later) calls this same command.
/// It has no endpoint.
/// </summary>
internal sealed class ValidatePackagingFileHandler(
    IPackagingSubmissionRepository repository,
    IPackagingFileValidator validator,
    TimeProvider timeProvider)
    : ICommandHandler<ValidatePackagingFileCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ValidatePackagingFileCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetByFileIdAsync(command.FileId, cancellationToken);
        var file = submission?.Files.SingleOrDefault(f => f.Id == command.FileId);

        if (submission is null || file is null)
            return PackagingErrors.FileNotFound;

        if (file.Status != PackagingFileStatus.Validating)
            return PackagingErrors.FileNotAwaitingValidation(file.Status);

        var check = await validator.CheckAsync(
            new StoredFile(file.FileName!, file.S3Bucket!, file.S3Key!), cancellationToken);

        var recorded = submission.RecordFileValidated(file, check, timeProvider.GetUtcNow());
        if (recorded.IsFailure)
            return recorded.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Unit.Value : saved.Error;
    }
}
