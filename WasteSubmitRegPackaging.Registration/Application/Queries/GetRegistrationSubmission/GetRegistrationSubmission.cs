using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.Registration.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Queries.GetRegistrationSubmission;

internal sealed record GetRegistrationSubmissionQuery(RegistrationSubmissionId RegistrationSubmissionId)
    : IQuery<RegistrationSubmissionView>;

/// <summary>
/// The view both UIs render. Status and the available regulator actions come from the backend, so
/// neither frontend works out rules itself. Today there are five copies of the status logic.
/// </summary>
internal sealed record RegistrationSubmissionView(
    Guid Id,
    Guid OrganisationId,
    Guid? ComplianceSchemeId,
    int RegistrationYear,
    RegistrationJourney Journey,
    Nation Nation,
    string ApplicationReferenceNumber,
    string? RegistrationReferenceNumber,
    RegistrationStatus Status,
    DateTimeOffset? RegistrationDate,
    IReadOnlyList<RegulatorDecision> AvailableRegulatorDecisions,
    IReadOnlyList<RegistrationHistoryItem> History);

internal sealed record RegistrationHistoryItem(
    RegistrationEventType EventType,
    ActorType ActorType,
    Guid ActorUserId,
    string? Comments,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset OccurredAt);

/// <summary>
/// Queries read the DbContext directly with no tracking, and skip the repository. Here we load the
/// aggregate, because the available actions are a domain rule. A list query (the regulator list, paged
/// and filtered) would project straight to rows in SQL instead.
/// </summary>
internal sealed class GetRegistrationSubmissionHandler(RegistrationDbContext dbContext)
    : IQueryHandler<GetRegistrationSubmissionQuery, RegistrationSubmissionView>
{
    public async Task<Result<RegistrationSubmissionView>> HandleAsync(
        GetRegistrationSubmissionQuery query, CancellationToken cancellationToken)
    {
        var submission = await dbContext.RegistrationSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == query.RegistrationSubmissionId, cancellationToken);

        if (submission is null)
            return RegistrationErrors.NotFound(query.RegistrationSubmissionId);

        return new RegistrationSubmissionView(
            submission.Id.Value,
            submission.OrganisationId,
            submission.ComplianceSchemeId,
            submission.RegistrationYear,
            submission.Journey,
            submission.Nation,
            submission.ApplicationReferenceNumber,
            submission.RegistrationReferenceNumber,
            submission.Status,
            submission.RegistrationDate,
            submission.AvailableRegulatorDecisions(),
            submission.History
                .OrderBy(item => item.OccurredAt)
                .Select(item => new RegistrationHistoryItem(
                    item.EventType, item.ActorType, item.ActorUserId, item.Comments, item.EffectiveDate, item.OccurredAt))
                .ToList());
    }
}
