using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Registration.Contracts;
using WasteSubmitRegPackaging.Registration.Domain;

namespace WasteSubmitRegPackaging.Registration.Infrastructure;

/// <summary>Implements the public contract on top of the module's own data. It is read-only.</summary>
internal sealed class RegistrationModuleApi(RegistrationDbContext dbContext) : IRegistrationModuleApi
{
    public async Task<RegistrationSummary?> GetLatestRegistrationAsync(
        Guid organisationId, int registrationYear, CancellationToken cancellationToken)
    {
        var latest = await dbContext.RegistrationSubmissions
            .AsNoTracking()
            .Where(s => s.OrganisationId == organisationId && s.RegistrationYear == registrationYear)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.RegistrationYear, s.Status, s.RegistrationReferenceNumber })
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null
            ? null
            : new RegistrationSummary(latest.Id.Value, latest.RegistrationYear, ToContract(latest.Status), latest.RegistrationReferenceNumber);
    }

    private static RegistrationState ToContract(RegistrationStatus status) => status switch
    {
        RegistrationStatus.InProgress => RegistrationState.InProgress,
        RegistrationStatus.SubmittedForApproval => RegistrationState.SubmittedForApproval,
        RegistrationStatus.Queried => RegistrationState.Queried,
        RegistrationStatus.Granted => RegistrationState.Granted,
        RegistrationStatus.Refused => RegistrationState.Refused,
        RegistrationStatus.Cancelled => RegistrationState.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
