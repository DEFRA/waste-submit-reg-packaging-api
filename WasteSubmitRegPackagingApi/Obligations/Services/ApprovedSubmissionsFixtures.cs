using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Example data: a direct registrant and a compliance scheme, both for packaging year 2024.
/// Codes are given as the raw registration file values and go through the mapper, as real data will.
/// </summary>
internal static class ApprovedSubmissionsFixtures
{
    private const int PackagingYear = 2024;

    public static Guid DirectRegistrantId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public static Guid ComplianceSchemeId { get; } = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    public static ItemsResponse<OrganisationPackaging>? Find(Guid organisationId, int packagingYear)
    {
        if (packagingYear != PackagingYear)
        {
            return null;
        }

        if (organisationId == DirectRegistrantId)
        {
            return DirectRegistrant;
        }

        return organisationId == ComplianceSchemeId ? ComplianceScheme : null;
    }

    private static ItemsResponse<OrganisationPackaging> DirectRegistrant => new()
    {
        Items =
        [
            Organisation(DirectRegistrantId, rawCode: null, joinerDate: null, leaverDate: null,
                Material("PL", 412350),
                Material("PC", 655120),
                Material("GL", 167800))
        ]
    };

    private static ItemsResponse<OrganisationPackaging> ComplianceScheme => new()
    {
        Items =
        [
            Organisation(Guid.Parse("22222222-2222-4222-8222-222222222222"), rawCode: null, joinerDate: null, leaverDate: null,
                Material("PL", 100000),
                Material("WD", 50000),
                Material("AL", 25000),
                Material("ST", 40000),
                Material("PC", 150000),
                Material("GL", 200000),
                Material("FC", 30000)),
            // Joined the scheme part-way through the relevant year (2025).
            Organisation(Guid.Parse("33333333-3333-4333-8333-333333333333"), rawCode: "03", joinerDate: new DateOnly(2025, 7, 5), leaverDate: null,
                Material("PL", 75000),
                Material("PC", 120000)),
            // Resigned from the scheme part-way through the relevant year.
            Organisation(Guid.Parse("44444444-4444-4444-8444-444444444444"), rawCode: "13", joinerDate: null, leaverDate: new DateOnly(2025, 9, 30),
                Material("GL", 64000))
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

    private static MaterialWeight Material(string materialCode, long weight) =>
        new() { MaterialCode = materialCode, Weight = weight };
}