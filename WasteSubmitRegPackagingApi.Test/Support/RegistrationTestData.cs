using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackagingApi.Test.Support;

internal static class RegistrationTestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Reference numbers are unique in the database, so tests that share one pass their own
    /// (<see cref="NewReferenceNumber"/>).
    /// </summary>
    public static RegistrationSubmission Started(string applicationReferenceNumber = "pepr00012345") =>
        RegistrationSubmission.Start(
            TestUsers.Producer,
            TestUsers.ProducerOrganisationId,
            complianceSchemeId: null,
            registrationYear: 2026,
            RegistrationJourney.DirectLargeProducer,
            Nation.England,
            applicationReferenceNumber,
            Now).Value;

    public static RegistrationSubmission SubmittedForApproval(string applicationReferenceNumber = "pepr00012345")
    {
        var submission = Started(applicationReferenceNumber);
        submission.SubmitApplication(TestUsers.Producer, "Additional information", Now).ShouldSucceed();
        return submission;
    }

    public static string NewReferenceNumber() => $"PEPR{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    public static void ShouldSucceed(this Result result) =>
        Assert.True(result.IsSuccess, result.Error?.Code);
}
