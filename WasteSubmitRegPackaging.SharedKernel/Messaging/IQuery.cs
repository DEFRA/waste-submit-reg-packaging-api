using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.SharedKernel.Messaging;

/// <summary>
/// A request to read state. Query handlers never change data, so they may read straight from the
/// module's DbContext (no tracking) and project into view models.
/// </summary>
public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
