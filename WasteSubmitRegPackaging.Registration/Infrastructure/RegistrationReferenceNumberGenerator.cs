using System.Globalization;
using System.Security.Cryptography;
using WasteSubmitRegPackaging.Registration.Domain;

namespace WasteSubmitRegPackaging.Registration.Infrastructure;

/// <summary>
/// SAMPLE format: <c>R{yy}{E|S|W|N}{P|C}{8 random digits}</c>. The real format must match today's (see
/// <c>OrganisationRegistationSubmissionService_Synchronisation.cs</c> in the regulator facade), because
/// the public register depends on it. A unique index on the column catches the rare clash.
/// </summary>
internal sealed class RegistrationReferenceNumberGenerator : IRegistrationReferenceNumberGenerator
{
    public string Generate(RegistrationSubmission submission)
    {
        var year = (submission.RegistrationYear % 100).ToString("00", CultureInfo.InvariantCulture);
        var nation = submission.Nation switch
        {
            Nation.England => 'E',
            Nation.Scotland => 'S',
            Nation.Wales => 'W',
            Nation.NorthernIreland => 'N',
            _ => throw new ArgumentOutOfRangeException(nameof(submission), submission.Nation, null)
        };
        var organisationType = submission.ComplianceSchemeId.HasValue ? 'C' : 'P';
        var suffix = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("00000000", CultureInfo.InvariantCulture);

        return $"R{year}{nation}{organisationType}{suffix}";
    }
}
