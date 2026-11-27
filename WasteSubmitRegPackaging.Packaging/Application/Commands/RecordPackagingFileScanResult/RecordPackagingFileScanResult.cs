using System.Security.Cryptography;
using System.Text;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingFileScanResult;

/// <param name="Stored">Where the uploader put the file, or null if it rejected it.</param>
/// <param name="RejectionReason">Why the uploader rejected the file, in its words.</param>
internal sealed record RecordPackagingFileScanResultCommand(
    Guid FileId, string CallbackToken, string? FileName, StoredFile? Stored, string? RejectionReason) : ICommand<Unit>;

/// <summary>
/// The CDP uploader's callback: the file has been scanned, and either stored in S3 or rejected.
///
/// The callback has no credentials and no user. The file id in the URL says which upload it is for, and so which
/// submission; the token in the URL proves the caller was given that URL. A repeated callback changes nothing.
///
/// A stored file is checked here, in the request. When validation is real and slow it will move to the background,
/// and the file will need a status between Uploading and Valid; see docs/packaging_workflow.md.
/// </summary>
internal sealed class RecordPackagingFileScanResultHandler(
    IPackagingSubmissionRepository repository,
    IPackagingFileValidator validator,
    TimeProvider timeProvider)
    : ICommandHandler<RecordPackagingFileScanResultCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RecordPackagingFileScanResultCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetByFileIdAsync(command.FileId, cancellationToken);
        var file = submission?.Files.SingleOrDefault(f => f.Id == command.FileId);

        if (submission is null || file is null || !TokensMatch(file.CallbackToken, command.CallbackToken))
            return PackagingErrors.FileNotFound;

        if (file.Status != PackagingFileStatus.Uploading)
            return Unit.Value;

        var now = timeProvider.GetUtcNow();

        if (command.Stored is { } stored)
            submission.RecordFileStored(file, stored, await validator.CheckAsync(stored, cancellationToken), now);
        else
            submission.RecordFileRejected(file, command.FileName, command.RejectionReason, now);

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Unit.Value : saved.Error;
    }

    private static bool TokensMatch(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
