using System.ComponentModel.DataAnnotations;
using WasteSubmitRegPackaging.Packaging.Domain;

namespace WasteSubmitRegPackaging.Packaging.Endpoints;

// Request shapes: public, with nullable value types and [Required], as Registration's (see RegistrationRequests.cs
// for why). Only shape rules go here; business rules live in handlers and the domain.

public sealed class StartPackagingFileUploadRequest
{
    [Required]
    public Guid? OrganisationId { get; init; }

    /// <summary>Set when a compliance scheme submits for its members.</summary>
    public Guid? ComplianceSchemeId { get; init; }

    /// <summary>The submission period's name, for example "July to December 2025".</summary>
    [Required, StringLength(100)]
    public string? SubmissionPeriod { get; init; }
}

/// <param name="CallbackPath">
/// The path on this API to give the CDP uploader as its <c>callback</c>, after this API's base URL. It holds a secret:
/// don't log it or send it to the browser.
/// </param>
internal sealed record StartPackagingFileUploadResponse(Guid PackagingSubmissionId, Guid FileId, string CallbackPath);

/// <summary>
/// The CDP uploader's callback payload (cdp-documentation, how-to/file-upload.md). Only the fields used are bound.
/// The upload form's file input must be named <c>file</c>.
/// </summary>
public sealed class PackagingFileScanResultRequest
{
    [Required]
    public PackagingFileScanResultFormRequest? Form { get; init; }
}

public sealed class PackagingFileScanResultFormRequest
{
    [Required]
    public PackagingFileScanResultFileRequest? File { get; init; }
}

public sealed class PackagingFileScanResultFileRequest
{
    /// <summary><c>complete</c> when the file was stored; anything else (<c>rejected</c>) means it wasn't.</summary>
    [Required]
    public string? FileStatus { get; init; }

    [StringLength(260)]
    public string? Filename { get; init; }

    [StringLength(100)]
    public string? S3Bucket { get; init; }

    [StringLength(1024)]
    public string? S3Key { get; init; }

    [StringLength(1000)]
    public string? ErrorMessage { get; init; }
}

public sealed class RecordPackagingResubmissionFeePaymentRequest
{
    /// <summary>PLACEHOLDER: the reference of the payment for this cycle's resubmission fee.</summary>
    [Required, StringLength(100)]
    public string? PaymentReference { get; init; }
}

public sealed class SubmitPackagingSubmissionRequest
{
    /// <summary>The file the producer checked. A newer upload makes this a 409.</summary>
    [Required]
    public Guid? FileId { get; init; }

    /// <summary>The name given on the declaration.</summary>
    [Required, StringLength(200)]
    public string? SubmittedBy { get; init; }
}

public sealed class RecordPackagingRegulatorDecisionRequest
{
    [Required, EnumDataType(typeof(PackagingRegulatorDecision))]
    public PackagingRegulatorDecision? Decision { get; init; }

    [StringLength(4000)]
    public string? Comments { get; init; }
}
