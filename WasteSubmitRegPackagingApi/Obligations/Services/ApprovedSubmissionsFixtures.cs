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
            Organisation(DirectRegistrantId, events: [],
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
            Organisation(Guid.Parse("22222222-2222-4222-8222-222222222222"), events: [],
                Material("PL", 100m),
                Material("WD", 50m),
                Material("AL", 25m),
                Material("ST", 40m),
                Material("PC", 150m),
                Material("GL", 200m),
                Material("FC", 30m)),
            // Joined the scheme part-way through the relevant year (2025).
            Organisation(Guid.Parse("33333333-3333-4333-8333-333333333333"), events: [Event("03", new DateOnly(2025, 7, 5))],
                Material("PL", 75m),
                Material("PC", 120m)),
            // Resigned from the scheme part-way through the relevant year.
            Organisation(Guid.Parse("44444444-4444-4444-8444-444444444444"), events: [Event("13", new DateOnly(2025, 9, 30))],
                Material("GL", 64m)),
            // Joined, resigned and rejoined within the relevant year.
            Organisation(Guid.Parse("55555555-5555-4555-8555-555555555555"),
                events:
                [
                    Event("01", new DateOnly(2025, 3, 3)),
                    Event("13", new DateOnly(2025, 7, 14)),
                    Event("01", new DateOnly(2025, 10, 1))
                ],
                Material("PL", 40m),
                Material("GL", 22m))
        ]
    };

    private static OrganisationPackaging Organisation(
        Guid organisationId,
        IReadOnlyList<LeaverJoinerEvent> events,
        params MaterialWeight[] materials) =>
        new()
        {
            OrganisationId = organisationId,
            Events = events.OrderBy(leaverJoinerEvent => leaverJoinerEvent.Date).ToList(),
            Materials = materials
        };

    private static LeaverJoinerEvent Event(string rawCode, DateOnly date)
    {
        var code = LeaverJoinerCodeMapper.Map(rawCode)
            ?? throw new ArgumentException($"Not a joiner or leaver code: {rawCode}", nameof(rawCode));

        return new LeaverJoinerEvent { Code = code.Code, Action = code.Action, Date = date };
    }

    private static MaterialWeight Material(string materialCode, decimal tonnes) =>
        new() { MaterialCode = materialCode, Tonnes = tonnes };
}