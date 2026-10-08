namespace WasteSubmitRegPackagingApi.Obligations.Models;

/// <summary>
/// Leaver codes from the registration file's leaver_code column, numbered and worded as in
/// https://www.gov.uk/guidance/register-for-epr-for-packaging-create-your-registration-file.
/// Serialised as the number.
/// </summary>
public enum LeaverCode
{
    NotIndependentlyObligatedLeftGroupPreviouslyRegisteredIndependently = 4,
    NotIndependentlyObligatedLeftGroupPreviouslyInGroupRegistration = 5,
    IndependentlyObligatedLeftGroupHoldingCompanyResponsible = 6,
    LeftGroupNowInDifferentGroupRegistration = 8,
    LeftGroupNowRegisteredIndependently = 10,
    InsolventAndStoppedTrading = 11,
    StoppedBeingProducer = 12,
    ResignedFromComplianceScheme = 13,
    ComplianceSchemeTerminatedMembership = 14,
    MergedWithAnotherOrganisation = 16,
    OtherLeaverReason = 21
}