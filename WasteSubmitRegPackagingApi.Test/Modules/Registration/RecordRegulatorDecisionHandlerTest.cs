using NSubstitute;
using WasteSubmitRegPackaging.Registration.Application.Commands.RecordRegulatorDecision;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackagingApi.Test.Modules.Registration;

/// <summary>
/// Handler tests: application rules (who, required inputs, not found) and that a change is saved.
/// The repository is mocked; the domain is real.
/// </summary>
public class RecordRegulatorDecisionHandlerTest
{
    private readonly IRegistrationSubmissionRepository _repository = Substitute.For<IRegistrationSubmissionRepository>();
    private readonly RecordRegulatorDecisionHandler _handler;

    public RecordRegulatorDecisionHandlerTest()
    {
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _handler = new RecordRegulatorDecisionHandler(
            _repository, new FixedReferenceNumberGenerator(), new FixedTimeProvider(RegistrationTestData.Now));
    }

    [Fact]
    public async Task Only_a_regulator_can_decide()
    {
        var result = await _handler.HandleAsync(Command(RegulatorDecision.Grant) with { Actor = TestActors.Producer }, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error?.Type);
        await _repository.DidNotReceive().GetAsync(Arg.Any<RegistrationSubmissionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_query_needs_comments()
    {
        var result = await _handler.HandleAsync(Command(RegulatorDecision.Query, comments: " "), CancellationToken.None);

        Assert.Equal("registration.comments_required", result.Error?.Code);
    }

    [Fact]
    public async Task An_unknown_registration_is_not_found()
    {
        _repository.GetAsync(Arg.Any<RegistrationSubmissionId>(), Arg.Any<CancellationToken>()).Returns((RegistrationSubmission?)null);

        var result = await _handler.HandleAsync(Command(RegulatorDecision.Grant), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task A_grant_is_saved_and_returns_the_reference_number()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.GetAsync(submission.Id, Arg.Any<CancellationToken>()).Returns(submission);

        var result = await _handler.HandleAsync(Command(RegulatorDecision.Grant, id: submission.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new RecordRegulatorDecisionResult(RegistrationStatus.Granted, "R26EP00000001"), result.Value);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_save_is_returned_rather_than_thrown()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.GetAsync(submission.Id, Arg.Any<CancellationToken>()).Returns(submission);
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(RegistrationErrors.ConcurrentChange));

        var result = await _handler.HandleAsync(Command(RegulatorDecision.Grant, id: submission.Id), CancellationToken.None);

        Assert.Equal("registration.concurrent_change", result.Error?.Code);
    }

    private static RecordRegulatorDecisionCommand Command(
        RegulatorDecision decision, string? comments = null, RegistrationSubmissionId? id = null) =>
        new(TestActors.Regulator, id ?? RegistrationSubmissionId.New(), decision, comments, null);
}
