using NSubstitute;
using WasteSubmitRegPackaging.Registration.Application.Commands.RecordRegulatorDecision;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackagingApi.Test.UnitTests.Registration;

/// <summary>
/// Handler tests: application rules (required inputs, not found) and that a change is saved.
/// The repository is mocked; the domain is real. "Only a regulator can decide" needs no test here: the
/// command takes a RegulatorUser, so a producer can't be passed. The endpoint test covers the 403.
/// </summary>
public class RecordRegulatorDecisionHandlerTest
{
    private readonly IRegistrationSubmissionRepository _repository = Substitute.For<IRegistrationSubmissionRepository>();
    private readonly RecordRegulatorDecisionHandler _handler;

    public RecordRegulatorDecisionHandlerTest()
    {
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _handler = new RecordRegulatorDecisionHandler(
            _repository, new FixedTimeProvider(RegistrationTestData.Now));
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
    public async Task A_grant_is_saved_and_returns_the_new_status()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.GetAsync(submission.Id, Arg.Any<CancellationToken>()).Returns(submission);

        var result = await _handler.HandleAsync(Command(RegulatorDecision.Grant, id: submission.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new RecordRegulatorDecisionResult(RegistrationStatus.Granted), result.Value);
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
        new(TestUsers.Regulator, id ?? RegistrationSubmissionId.New(), decision, comments, null);
}
