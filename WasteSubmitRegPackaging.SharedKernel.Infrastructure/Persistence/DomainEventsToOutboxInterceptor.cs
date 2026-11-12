using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// Moves the domain events raised by tracked aggregates into the module's outbox table, inside the same
/// <c>SaveChanges</c> and so the same transaction. A change and its notification are either both saved
/// or neither is, which today's "write, then send, then log on failure" approach cannot promise.
/// </summary>
public sealed class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteOutboxMessages(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        WriteOutboxMessages(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void WriteOutboxMessages(DbContext? context)
    {
        if (context is null)
            return;

        var aggregates = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(),
                    Type = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), s_jsonOptions),
                    OccurredAt = domainEvent.OccurredAt
                });
            }

            aggregate.ClearDomainEvents();
        }
    }
}
