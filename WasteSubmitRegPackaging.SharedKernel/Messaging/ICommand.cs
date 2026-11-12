using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.SharedKernel.Messaging;

/// <summary>
/// A request to change state. One command has exactly one handler, and the handler returns a
/// <see cref="Result{T}"/> rather than throwing for expected failures (validation, not found, conflict).
/// </summary>
/// <typeparam name="TResponse">What the caller gets back on success. Use <see cref="Unit"/> for nothing.</typeparam>
public interface ICommand<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
