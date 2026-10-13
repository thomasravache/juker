using Juker.SharedKernel;

namespace Juker.Infrastructure;

/// <summary>Handler de consulta; resolvido do DI já envolvido pelos decorators (validação e logging).</summary>
/// <typeparam name="TQuery">A consulta.</typeparam>
/// <typeparam name="TResult">Tipo do valor do resultado.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken ct);
}
