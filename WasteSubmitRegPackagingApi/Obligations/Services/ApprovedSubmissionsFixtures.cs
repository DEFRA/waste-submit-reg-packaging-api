using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Example data for packaging year 2024: a direct registrant and a compliance scheme.
/// Codes are given as the raw registration file values and go through the mapper, as real data will.
/// </summary>
internal static class ApprovedSubmissionsFixtures
{
    private const int PackagingYear = 2024;

    public static Guid DirectRegistrantId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public static Guid ComplianceSchemeId { get; } = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    public static IReadOnlyList<ApprovedSubmission>? ForYear(int year) =>
        year == PackagingYear ? [DirectRegistrant, ComplianceScheme] : null;

    private static ApprovedSubmission DirectRegistrant => new()
    {
        SubmitterId = DirectRegistrantId,
        SubmitterType = SubmitterType.DirectRegistrant,
        AggregatedPackaging =
        [
            Organisation(DirectRegistrantId, rawCode: null, joinerDate: null, leaverDate: null,
                Material("PL", 412.35m),
                Material("PC", 655.12m),
                Material("GL", 167.8m))
        ]
    };

    private static ApprovedSubmission ComplianceScheme => new()
    {
        SubmitterId = ComplianceSchemeId,
        SubmitterType = SubmitterType.ComplianceScheme,
        AggregatedPackaging =
        [
            Organisation(Guid.Parse("22222222-2222-4222-8222-222222222222"), rawCode: null, joinerDate: null, leaverDate: null,
                Material("PL", 100m),
                Material("WD", 50m),
                Material("AL", 25m),
                Material("ST", 40m),
                Material("PC", 150m),
                Material("GL", 200m),
                Material("FC", 30m)),
            // Joined the scheme part-way through the relevant year (2025).
            Organisation(Guid.Parse("33333333-3333-4333-8333-333333333333"), rawCode: "03", joinerDate: new DateOnly(2025, 7, 5), leaverDate: null,
                Material("PL", 75m),
                Material("PC", 120m)),
            // Resigned from the scheme part-way through the relevant year.
            Organisation(Guid.Parse("44444444-4444-4444-8444-444444444444"), rawCode: "13", joinerDate: null, leaverDate: new DateOnly(2025, 9, 30),
                Material("GL", 64m))
        ]
    };

    private static OrganisationPackaging Organisation(
        Guid organisationId,
        string? rawCode,
        DateOnly? joinerDate,
        DateOnly? leaverDate,
        params MaterialWeight[] materials)
    {
        var code = LeaverJoinerCodeMapper.Map(rawCode);

        return new OrganisationPackaging
        {
            OrganisationId = organisationId,
            JoinerCode = code.JoinerCode,
            JoinerDate = joinerDate,
            LeaverCode = code.LeaverCode,
            LeaverDate = leaverDate,
            Materials = materials
        };
    }

    private static MaterialWeight Material(string materialCode, decimal tonnes) =>
        new() { MaterialCode = materialCode, Tonnes = tonnes };
}