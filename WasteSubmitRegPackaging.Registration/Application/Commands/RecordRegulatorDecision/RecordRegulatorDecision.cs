using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Commands.RecordRegulatorDecision;

internal sealed record RecordRegulatorDecisionCommand(
    Actor Actor,
    RegistrationSubmissionId RegistrationSubmissionId,
    RegulatorDecision Decision,
    string? Comments,
    DateTimeOffset? CancellationEffectiveDate) : ICommand<RecordRegulatorDecisionResult>;

internal sealed record RecordRegulatorDecisionResult(RegistrationStatus Status, string? RegistrationReferenceNumber);

/// <summary>
/// Shows the three layers of validation:
/// <list type="number">
/// <item>Shape (required, lengths): the endpoint's request type, checked by <c>AddValidation()</c>. Returns 400.</item>
/// <item>Application rules (who, and cross-field rules): here. Returns 403 or 400.</item>
/// <item>Domain invariants (is this transition allowed?): <see cref="RegistrationSubmission.RecordDecision"/>. Returns 409.</item>
/// </list>
/// The decision, its history row and its outbox message (for the notification email) are saved in one
/// transaction.
/// </summary>
internal sealed class RecordRegulatorDecisionHandler(
    IRegistrationSubmissionRepository repository,
    IRegistrationReferenceNumberGenerator referenceNumberGenerator,
    TimeProvider timeProvider)
    : ICommandHandler<RecordRegulatorDecisionCommand, RecordRegulatorDecisionResult>
{
    public async Task<Result<RecordRegulatorDecisionResult>> HandleAsync(
        RecordRegulatorDecisionCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Type != ActorType.Regulator)
            return RegistrationErrors.OnlyRegulators;

        // Rule R5 in the roadmap is still open. Today's UI only limits comments to 400 characters.
        // Requiring them for Query is a SAMPLE rule, to show where such rules go.
        if (command.Decision == RegulatorDecision.Query && string.IsNullOrWhiteSpace(command.Comments))
            return RegistrationErrors.CommentsRequired;

        var submission = await repository.GetAsync(command.RegistrationSubmissionId, cancellationToken);
        if (submission is null)
            return RegistrationErrors.NotFound(command.RegistrationSubmissionId);

        // Still to add: check the regulator's nation matches the submission's nation (403).

        var decided = submission.RecordDecision(
            command.Decision,
            command.Actor,
            command.Comments,
            command.CancellationEffectiveDate,
            referenceNumberGenerator,
            timeProvider.GetUtcNow());

        if (decided.IsFailure)
            return decided.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess
            ? new RecordRegulatorDecisionResult(submission.Status, submission.RegistrationReferenceNumber)
            : saved.Error;
    }
}
