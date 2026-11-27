namespace WasteSubmitRegPackaging.Packaging.Domain;

/// <summary>What the uploader stored: the result of a successful scan.</summary>
internal sealed record StoredFile(string FileName, string S3Bucket, string S3Key);

/// <summary>The result of checking a stored file. A file with any errors can't be submitted; warnings don't block.</summary>
internal sealed record PackagingFileCheck(int ErrorCount, int WarningCount)
{
    public static readonly PackagingFileCheck Passed = new(0, 0);
}

/// <summary>
/// One upload (<c>pkg_submission_file</c>). The row is written when the upload starts, so the uploader's callback has
/// something to report to; the file name and where it is in S3 arrive with that callback.
/// </summary>
internal sealed class SubmissionFile
{
    private SubmissionFile()
    {
        // For EF Core.
    }

    internal SubmissionFile(Guid submissionCycleId, string callbackToken, Guid startedByUserId, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        SubmissionCycleId = submissionCycleId;
        CallbackToken = callbackToken;
        Status = PackagingFileStatus.Uploading;
        StartedByUserId = startedByUserId;
        StartedAt = now;
    }

    public Guid Id { get; private init; }

    /// <summary>The cycle this file was uploaded in.</summary>
    public Guid SubmissionCycleId { get; private init; }

    public PackagingFileStatus Status { get; private set; }

    /// <summary>The secret in the callback URL. The uploader's callback carries no credentials, so this is what proves it.</summary>
    public string CallbackToken { get; private init; } = null!;

    public string? FileName { get; private set; }

    public string? S3Bucket { get; private set; }

    public string? S3Key { get; private set; }

    /// <summary>Why the uploader rejected the file, in its words.</summary>
    public string? RejectionReason { get; private set; }

    public int? ErrorCount { get; private set; }

    public int? WarningCount { get; private set; }

    /// <summary>The callback has no user, so the user is recorded when the upload starts.</summary>
    public Guid StartedByUserId { get; private init; }

    public DateTimeOffset StartedAt { get; private init; }

    /// <summary>When the uploader reported back.</summary>
    public DateTimeOffset? ScannedAt { get; private set; }

    internal void Store(StoredFile stored, PackagingFileCheck check, DateTimeOffset now)
    {
        FileName = stored.FileName;
        S3Bucket = stored.S3Bucket;
        S3Key = stored.S3Key;
        ErrorCount = check.ErrorCount;
        WarningCount = check.WarningCount;
        Status = check.ErrorCount > 0 ? PackagingFileStatus.Invalid : PackagingFileStatus.Valid;
        ScannedAt = now;
    }

    internal void Reject(string? fileName, string? reason, DateTimeOffset now)
    {
        FileName = fileName;
        RejectionReason = reason;
        Status = PackagingFileStatus.Rejected;
        ScannedAt = now;
    }
}
