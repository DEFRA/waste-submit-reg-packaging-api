using Microsoft.EntityFrameworkCore;
using Npgsql;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Infrastructure;

internal sealed class RegistrationSubmissionRepository(RegistrationDbContext dbContext) : IRegistrationSubmissionRepository
{
    public Task<RegistrationSubmission?> GetAsync(RegistrationSubmissionId id, CancellationToken cancellationToken) =>
        dbContext.RegistrationSubmissions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ApplicationReferenceNumberExistsAsync(string applicationReferenceNumber, CancellationToken cancellationToken)
    {
        var normalised = applicationReferenceNumber.Trim().ToUpperInvariant();
        return dbContext.RegistrationSubmissions.AnyAsync(s => s.ApplicationReferenceNumber == normalised, cancellationToken);
    }

    public void Add(RegistrationSubmission submission) => dbContext.RegistrationSubmissions.Add(submission);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return RegistrationErrors.ConcurrentChange;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests raced past the existence check. The unique index is the real guard.
            return RegistrationErrors.ApplicationReferenceNumberInUse;
        }
    }
}
