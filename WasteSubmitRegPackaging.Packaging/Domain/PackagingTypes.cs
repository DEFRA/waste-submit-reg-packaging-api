using System.Text.Json.Serialization;

namespace WasteSubmitRegPackaging.Packaging.Domain;

internal readonly record struct PackagingSubmissionId(Guid Value)
{
    public static PackagingSubmissionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

// Enums are stored as strings, with CHECK constraints in the Liquibase changelog, and serialised as strings in the
// API. Adding a value means changing both. The status and decision names are the ones EPR uses today
// (SubmissionPeriodStatus, RegulatorDecision).
//
// The enums used in request types are public, because the validation generator needs every type a request exposes
// to be public. Everything else in Domain stays internal.

/// <summary>
/// The one status of a packaging submission. There is no "not started": that is a submitter and period with no row.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PackagingSubmissionStatus>))]
internal enum PackagingSubmissionStatus
{
    /// <summary>A cycle is open: the producer is uploading, fixing or about to submit.</summary>
    InProgress,
    SubmittedToRegulator,
    AcceptedByRegulator,
    RejectedByRegulator
}

/// <summary>What happened to one upload.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PackagingFileStatus>))]
internal enum PackagingFileStatus
{
    /// <summary>The upload was started. The uploader hasn't reported back yet.</summary>
    Uploading,

    /// <summary>The uploader rejected it, for example for a virus. Nothing was stored.</summary>
    Rejected,

    /// <summary>Stored in S3 and waiting for validation, or being validated. Validation is a step of its own.</summary>
    Validating,

    /// <summary>Validated, and has errors. It can't be submitted.</summary>
    Invalid,

    /// <summary>Validated with no errors. It may have warnings.</summary>
    Valid
}

/// <summary>
/// What the producer has to do next. The frontend routes on this and never works the step out for itself.
/// Null when there is nothing for the producer to do; the status says why.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PackagingNextStep>))]
internal enum PackagingNextStep
{
    UploadFile,
    AwaitFileCheck,
    FixErrors,

    /// <summary>Resubmission cycles only.</summary>
    PayResubmissionFee,
    SubmitToRegulator
}

[JsonConverter(typeof(JsonStringEnumConverter<PackagingRegulatorDecision>))]
public enum PackagingRegulatorDecision
{
    Accepted,
    Rejected
}
