using NSubstitute;
using WasteSubmitRegPackaging.Packaging.Application.Commands.CreatePackagingSubmission;
using WasteSubmitRegPackaging.Packaging.Domain;

namespace WasteSubmitRegPackagingApi.Test.Unit.Packaging;

public class CreatePackagingSubmissionHandlerTest
{
    private readonly IPackagingSubmissionRepository _repository = Substitute.For<IPackagingSubmissionRepository>();
    private readonly CreatePackagingSubmissionHandler _handler;

    public CreatePackagingSubmissionHandlerTest() =>
        _handler = new CreatePackagingSubmissionHandler(_repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task A_producer_gets_a_draft_submission_for_the_following_registration_year_without_registering_first()
    {
        var result = await _handler.HandleAsync(Command(TestUsers.ProducerOrganisationId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Received(1).Add(Arg.Is<PackagingSubmission>(s =>
            s.PackagingYear == 2026 && s.RegistrationYear == 2027 && s.Status == PackagingSubmissionStatus.Draft));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_producer_cannot_submit_for_another_organisation()
    {
        var result = await _handler.HandleAsync(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("packaging.not_your_organisation", result.Error?.Code);
        _repository.DidNotReceive().Add(Arg.Any<PackagingSubmission>());
    }

    private static CreatePackagingSubmissionCommand Command(Guid organisationId) =>
        new(TestUsers.Producer, organisationId, 2026);
}
