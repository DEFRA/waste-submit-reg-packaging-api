using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Commands.RecordRegulatorDecision;

internal sealed record RecordRegulatorDecisionCommand(
    RegulatorUser Regulator,
    RegistrationSubmissionId RegistrationSubmissionId,
    RegulatorDecision Decision,
    string? Comments,
    DateTimeOffset? CancellationEffectiveDate) : ICommand<RecordRegulatorDecisionResult>;

internal sealed record RecordRegulatorDecisionResult(RegistrationStatus Status);

/// <summary>
/// Shows the three layers of validation:
/// <list type="number">
/// <item>Shape (required, lengths): the endpoint's request type, checked by <c>AddValidation()</c>. Returns 400.</item>
/// <item>Application rules (cross-field rules, anything needing the database): here. Returns 400 or 404.</item>
/// <item>Domain invariants (is this transition allowed?): <see cref="RegistrationSubmission.RecordDecision"/>. Returns 409.</item>
/// </list>
/// The decision, its history row and its outbox message (for the notification email) are saved in one
/// transaction.
/// </summary>
internal sealed class RecordRegulatorDecisionHandler(
    IRegistrationSubmissionRepository repository,
    TimeProvider timeProvider)
    : ICommandHandler<RecordRegulatorDecisionCommand, RecordRegulatorDecisionResult>
{
    public async Task<Result<RecordRegulatorDecisionResult>> HandleAsync(
        RecordRegulatorDecisionCommand command, CancellationToken cancellationToken)
    {
        // Only a regulator gets here: the command needs a RegulatorUser, and the endpoint's
        // RequireRegulator() turns a producer away with a 403.

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
            command.Regulator,
            command.Comments,
            command.CancellationEffectiveDate,
            timeProvider.GetUtcNow());

        if (decided.IsFailure)
            return decided.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess
            ? new RecordRegulatorDecisionResult(submission.Status)
            : saved.Error;
    }
}
