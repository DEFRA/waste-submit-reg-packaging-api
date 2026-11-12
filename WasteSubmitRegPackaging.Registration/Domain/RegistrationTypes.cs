using System.Text.Json.Serialization;

namespace WasteSubmitRegPackaging.Registration.Domain;

internal readonly record struct RegistrationSubmissionId(Guid Value)
{
    public static RegistrationSubmissionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

// Enums are stored as strings, with CHECK constraints in the Liquibase changelog, and serialised as
// strings in the API. Adding a value means changing both.
//
// The enums used in request types are public, because the validation generator needs every type a
// request exposes to be public. Everything else in Domain stays internal.

[JsonConverter(typeof(JsonStringEnumConverter<RegistrationStatus>))]
internal enum RegistrationStatus
{
    InProgress,
    SubmittedForApproval,
    Queried,
    Granted,
    Refused,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter<RegulatorDecision>))]
public enum RegulatorDecision
{
    Grant,
    Refuse,
    Query,
    Cancel
}

[JsonConverter(typeof(JsonStringEnumConverter<RegistrationJourney>))]
public enum RegistrationJourney
{
    DirectLargeProducer,
    DirectSmallProducer,
    CsoLargeProducer,
    CsoSmallProducer,
    CsoLegacy
}

/// <summary>
/// The regulator nations. The numbers are the ids in the Nations reference table, and are what the
/// database stores (<c>nation_id</c>), so data carried over from Azure maps straight across. The API uses
/// the ISO 3166-2 codes (GB-ENG and so on), and also accepts the ids.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Nation>))]
public enum Nation
{
    [JsonStringEnumMemberName("GB-ENG")] England = 1,
    [JsonStringEnumMemberName("GB-SCT")] Scotland = 2,
    [JsonStringEnumMemberName("GB-WLS")] Wales = 3,
    [JsonStringEnumMemberName("GB-NIR")] NorthernIreland = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<RegistrationEventType>))]
internal enum RegistrationEventType
{
    Started,
    ApplicationSubmitted,
    Granted,
    Refused,
    Queried,
    Cancelled
}

internal static class RegistrationJourneyExtensions
{
    public static bool IsComplianceScheme(this RegistrationJourney journey) =>
        journey is RegistrationJourney.CsoLargeProducer or RegistrationJourney.CsoSmallProducer or RegistrationJourney.CsoLegacy;
}
