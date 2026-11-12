using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WasteSubmitRegPackaging.Registration.Domain;

namespace WasteSubmitRegPackaging.Registration.Infrastructure;

/// <summary>
/// The EF mapping. Column names come from the snake_case convention and must match
/// <c>changelog/changes/registration/0001-registration-submission.xml</c>; the database test checks this.
/// </summary>
internal sealed class RegistrationSubmissionConfiguration : IEntityTypeConfiguration<RegistrationSubmission>
{
    public void Configure(EntityTypeBuilder<RegistrationSubmission> builder)
    {
        builder.ToTable("reg_registration_submission");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new RegistrationSubmissionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.ApplicationReferenceNumber).HasMaxLength(50);
        builder.Property(s => s.RegistrationReferenceNumber).HasMaxLength(50);
        builder.Property(s => s.Journey).HasConversion<string>().HasMaxLength(40);
        // Stored as the Nations reference table id (1 to 4), not as text. See Nation.
        builder.Property(s => s.Nation).HasColumnName("nation_id").HasConversion<short>();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(40);

        // Optimistic concurrency on Postgres's xmin system column. Two regulators deciding at once means
        // one save fails, and the repository turns that into a 409. No extra column is needed.
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsMany(s => s.History, history =>
        {
            history.ToTable("reg_registration_submission_event");
            history.WithOwner().HasForeignKey("RegistrationSubmissionId");
            history.HasKey(e => e.Id);

            // The ID is set in code, so EF treats a new entry in the list as an insert.
            history.Property(e => e.Id).ValueGeneratedNever();
            history.Property(e => e.EventType).HasConversion<string>().HasMaxLength(40);
            history.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(20);
            history.Property(e => e.Comments).HasMaxLength(4000);
        });

        builder.Navigation(s => s.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Aggregates raise domain events in memory only.
        builder.Ignore(s => s.DomainEvents);
    }
}
