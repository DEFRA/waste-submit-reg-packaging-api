using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

namespace WasteSubmitRegPackaging.Registration.Infrastructure;

/// <summary>Maps only the Registration module's <c>reg_*</c> tables.</summary>
internal sealed class RegistrationDbContext(DbContextOptions<RegistrationDbContext> options) : ModuleDbContext(options)
{
    public override string OutboxTableName => "reg_outbox_message";

    public DbSet<RegistrationSubmission> RegistrationSubmissions => Set<RegistrationSubmission>();
}
