using NSubstitute;
using WasteSubmitRegPackaging.Packaging.Application.Commands.CreatePackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Registration.Contracts;

namespace WasteSubmitRegPackagingApi.Test.Modules.Packaging;

/// <summary>Packaging tests mock the Registration contract, never Registration's internals.</summary>
public class CreatePackagingSubmissionHandlerTest
{
    private readonly IPackagingSubmissionRepository _repository = Substitute.For<IPackagingSubmissionRepository>();
    private readonly IRegistrationModuleApi _registrations = Substitute.For<IRegistrationModuleApi>();
    private readonly CreatePackagingSubmissionHandler _handler;

    public CreatePackagingSubmissionHandlerTest() =>
        _handler = new CreatePackagingSubmissionHandler(_repository, _registrations, new FixedTimeProvider(DateTimeOffset.UnixEpoch));

    [Theory]
    [InlineData(null)]
    [InlineData(RegistrationState.SubmittedForApproval)]
    [InlineData(RegistrationState.Cancelled)]
    public async Task An_organisation_without_a_granted_registration_cannot_submit(RegistrationState? state)
    {
        _registrations.GetLatestRegistrationAsync(TestActors.ProducerOrganisationId, 2026, Arg.Any<CancellationToken>())
            .Returns(state is null ? null : new RegistrationSummary(Guid.NewGuid(), 2026, state.Value, null));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        Assert.Equal("packaging.not_registered", result.Error?.Code);
        _repository.DidNotReceive().Add(Arg.Any<PackagingSubmission>());
    }

    [Fact]
    public async Task A_registered_organisation_gets_a_draft_submission_linked_to_its_registration()
    {
        var registrationId = Guid.NewGuid();
        _registrations.GetLatestRegistrationAsync(TestActors.ProducerOrganisationId, 2026, Arg.Any<CancellationToken>())
            .Returns(new RegistrationSummary(registrationId, 2026, RegistrationState.Granted, "R26EP00000001"));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Received(1).Add(Arg.Is<PackagingSubmission>(s =>
            s.RegistrationSubmissionId == registrationId && s.Status == PackagingSubmissionStatus.Draft));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CreatePackagingSubmissionCommand Command() =>
        new(TestActors.Producer, TestActors.ProducerOrganisationId, 2026);
}
