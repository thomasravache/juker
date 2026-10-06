namespace Juker.Infrastructure;

/// <summary>Comando de um slice, despachado por um <see cref="ICommandHandler{TCommand, TResult}"/>.</summary>
/// <typeparam name="TResult">Tipo do valor do resultado.</typeparam>
public interface ICommand<TResult>;
