namespace Juker.Infrastructure;

/// <summary>Consulta de um slice, despachada por um <see cref="IQueryHandler{TQuery, TResult}"/>.</summary>
/// <typeparam name="TResult">Tipo do valor do resultado.</typeparam>
public interface IQuery<TResult>;
