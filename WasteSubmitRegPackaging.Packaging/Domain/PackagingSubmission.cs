using System.Text.Json.Serialization;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Domain;

internal readonly record struct PackagingSubmissionId(Guid Value)
{
    public static PackagingSubmissionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(JsonStringEnumConverter<PackagingSubmissionStatus>))]
internal enum PackagingSubmissionStatus
{
    Draft,
    Submitted
}

internal sealed record PackagingSubmissionCreated(
    Guid PackagingSubmissionId, Guid OrganisationId, int PackagingYear, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// SAMPLE aggregate. It exists only to show the shape: a strongly typed ID, private setters, a factory
/// method that returns a <see cref="Result"/>, and a domain event. The packaging team owns the real model.
/// </summary>
internal sealed class PackagingSubmission : AggregateRoot<PackagingSubmissionId>
{
    private PackagingSubmission()
    {
        // For EF Core.
    }

    public Guid OrganisationId { get; private set; }

    public int PackagingYear { get; private set; }

    /// <summary>
    /// The registration year this data is reported before: <see cref="PackagingYear"/> + 1 (report your 2025 packaging
    /// data before registering for 2026). SAMPLE rule, the same as the seeded submission periods; to confirm with the
    /// business. A year, not a registration id, because producers report packaging data before they register.
    /// </summary>
    public int RegistrationYear { get; private set; }

    public PackagingSubmissionStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static PackagingSubmission Create(
        ProducerUser createdBy, Guid organisationId, int packagingYear, DateTimeOffset now)
    {
        var submission = new PackagingSubmission
        {
            Id = PackagingSubmissionId.New(),
            OrganisationId = organisationId,
            PackagingYear = packagingYear,
            RegistrationYear = packagingYear + 1,
            Status = PackagingSubmissionStatus.Draft,
            CreatedByUserId = createdBy.UserId,
            CreatedAt = now
        };

        submission.Raise(new PackagingSubmissionCreated(submission.Id.Value, organisationId, packagingYear, now));
        return submission;
    }
}

internal static class PackagingErrors
{
    public static Error NotFound(PackagingSubmissionId id) =>
        Error.NotFound("packaging.not_found", $"No packaging submission with ID {id}.");

    public static readonly Error NotYourOrganisation =
        Error.Forbidden("packaging.not_your_organisation", "The user does not act for this organisation.");
}

internal interface IPackagingSubmissionRepository
{
    void Add(PackagingSubmission submission);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
