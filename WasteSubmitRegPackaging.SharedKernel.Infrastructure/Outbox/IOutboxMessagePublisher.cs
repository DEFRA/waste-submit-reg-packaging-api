using Microsoft.Extensions.Logging;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

/// <summary>
/// Sends an outbox message on: a GOV.UK Notify email, an SNS/SQS message, or a call to another module.
/// Publishing must be safe to repeat, because a message can be sent again if the app stops between
/// publishing it and marking it processed (at-least-once delivery).
/// </summary>
public interface IOutboxMessagePublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>The skeleton's only publisher: it just logs. Replace it per event type when real ones exist.</summary>
internal sealed partial class LoggingOutboxMessagePublisher(ILogger<LoggingOutboxMessagePublisher> logger) : IOutboxMessagePublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        LogPublished(message.Type, message.Id);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published outbox message {Type} {Id}")]
    private partial void LogPublished(string type, Guid id);
}
