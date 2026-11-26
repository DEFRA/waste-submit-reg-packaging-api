namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

/// <summary>Bound from the <c>Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Off by default, so tests and runs with no database don't poll. Switched on per environment in
    /// CDP app config.
    /// </summary>
    public bool DispatcherEnabled { get; init; }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; init; } = 20;

    /// <summary>After this many failed attempts a message is left for someone to look at.</summary>
    public int MaxAttempts { get; init; } = 10;
}
