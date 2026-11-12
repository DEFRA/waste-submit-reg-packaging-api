using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

/// <summary>
/// Polls one module's outbox table and publishes what it finds. Each module gets its own dispatcher.
///
/// <c>FOR UPDATE SKIP LOCKED</c> lets several container instances run this at the same time without
/// sending the same message twice in parallel.
/// </summary>
internal sealed partial class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DispatcherEnabled)
            return;

        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogBatchFailed(exception, typeof(TContext).Name);
            }
        }
    }

    internal async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxMessagePublisher>();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // The table name comes from the context, never from user input, so building the SQL is safe.
#pragma warning disable EF1002
        var messages = await context.Set<OutboxMessage>()
            .FromSqlRaw(
                $"""
                 SELECT * FROM {context.OutboxTableName}
                 WHERE processed_at IS NULL AND attempts < {options.Value.MaxAttempts}
                 ORDER BY occurred_at
                 LIMIT {options.Value.BatchSize}
                 FOR UPDATE SKIP LOCKED
                 """)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.ProcessedAt = timeProvider.GetUtcNow();
                message.Error = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Error = exception.Message;
            }

            message.Attempts++;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch failed for {Context}")]
    private partial void LogBatchFailed(Exception exception, string context);
}
