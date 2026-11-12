using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackagingApi.Test.Modules.Registration;

internal static class RegistrationTestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    public static RegistrationSubmission Started() =>
        RegistrationSubmission.Start(
            TestActors.Producer,
            TestActors.ProducerOrganisationId,
            complianceSchemeId: null,
            registrationYear: 2026,
            RegistrationJourney.DirectLargeProducer,
            Nation.England,
            "pepr00012345",
            Now).Value;

    public static RegistrationSubmission SubmittedForApproval()
    {
        var submission = Started();
        submission.SubmitApplication(TestActors.Producer, "Additional information", Now).ShouldSucceed();
        return submission;
    }

    public static void ShouldSucceed(this Result result) =>
        Assert.True(result.IsSuccess, result.Error?.Code);
}

internal sealed class FixedReferenceNumberGenerator(string referenceNumber = "R26EP00000001") : IRegistrationReferenceNumberGenerator
{
    public int Calls { get; private set; }

    public string Generate(RegistrationSubmission submission)
    {
        Calls++;
        return referenceNumber;
    }
}

/// <summary>
/// An in-memory repository, so the endpoint tests can run the real handlers and domain without Postgres.
/// </summary>
internal sealed class InMemoryRegistrationSubmissionRepository : IRegistrationSubmissionRepository
{
    private readonly Dictionary<RegistrationSubmissionId, RegistrationSubmission> _submissions = [];

    public int Saves { get; private set; }

    public void Seed(RegistrationSubmission submission) => _submissions[submission.Id] = submission;

    public Task<RegistrationSubmission?> GetAsync(RegistrationSubmissionId id, CancellationToken cancellationToken) =>
        Task.FromResult(_submissions.GetValueOrDefault(id));

    public Task<bool> ApplicationReferenceNumberExistsAsync(string applicationReferenceNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_submissions.Values.Any(s =>
            s.ApplicationReferenceNumber == applicationReferenceNumber.Trim().ToUpperInvariant()));

    public void Add(RegistrationSubmission submission) => _submissions[submission.Id] = submission;

    public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.FromResult(Result.Success());
    }
}
