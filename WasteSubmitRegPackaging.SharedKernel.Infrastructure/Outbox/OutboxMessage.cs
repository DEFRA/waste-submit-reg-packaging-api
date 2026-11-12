using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

/// <summary>A domain event waiting to be dispatched. Each module has its own outbox table.</summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>The event's type name, for example <c>RegistrationDecisionRecorded</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The event serialised as JSON.</summary>
    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }
}

internal sealed class OutboxMessageConfiguration(string tableName) : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(tableName);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(200);
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        builder.Property(message => message.Error).HasMaxLength(2000);
    }
}
