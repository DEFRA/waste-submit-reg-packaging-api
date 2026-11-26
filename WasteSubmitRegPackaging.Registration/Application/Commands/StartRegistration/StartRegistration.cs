using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Commands.StartRegistration;

// One use case per folder: the command, its handler and anything only they use. To find everything
// about "start a registration", open this folder.

internal sealed record StartRegistrationCommand(
    ProducerUser Producer,
    Guid OrganisationId,
    Guid? ComplianceSchemeId,
    int RegistrationYear,
    RegistrationJourney Journey,
    Nation Nation,
    string ApplicationReferenceNumber) : ICommand<RegistrationSubmissionId>;

internal sealed class StartRegistrationHandler(IRegistrationSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<StartRegistrationCommand, RegistrationSubmissionId>
{
    public async Task<Result<RegistrationSubmissionId>> HandleAsync(StartRegistrationCommand command, CancellationToken cancellationToken)
    {
        // Application rules: who may do this, and anything that needs the database. The request's
        // shape (required fields, lengths) was already checked at the endpoint.
        if (!command.Producer.ActsFor(command.OrganisationId))
            return RegistrationErrors.NotYourOrganisation;

        if (await repository.ApplicationReferenceNumberExistsAsync(command.ApplicationReferenceNumber, cancellationToken))
            return RegistrationErrors.ApplicationReferenceNumberInUse;

        // Domain rules: the aggregate checks its own invariants.
        var started = RegistrationSubmission.Start(
            command.Producer,
            command.OrganisationId,
            command.ComplianceSchemeId,
            command.RegistrationYear,
            command.Journey,
            command.Nation,
            command.ApplicationReferenceNumber,
            timeProvider.GetUtcNow());

        if (started.IsFailure)
            return started.Error;

        repository.Add(started.Value);

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? started.Value.Id : saved.Error;
    }
}
