namespace WasteSubmitRegPackagingApi.Obligations.Models;

/// <summary>
/// Joiner codes from the registration file's leaver_code column, numbered and worded as in
/// https://www.gov.uk/guidance/register-for-epr-for-packaging-create-your-registration-file.
/// Serialised as the number. Code 18 is accepted by the registration validator but is not in
/// the guidance, so it is left out until its meaning is agreed.
/// </summary>
public enum JoinerCode
{
    ObligatedProducerJoinedGroup = 1,
    PreviouslyNotObligatedProducerJoinedGroupRegisteringIndependently = 2,
    PreviouslyNotObligatedProducerJoinedGroupAsPartOfRegistration = 3,
    JoinedGroupHoldingCompanyNotResponsible = 7,
    JoinedGroupNotResponsibleNowRegisteredIndividually = 9,
    BecameObligatedDueToMidYearChange = 15,
    CarryingOnActivitiesOfIncapacitatedProducer = 17,
    RegisteredAfterDeadline = 19,
    OtherJoinerReason = 20
}