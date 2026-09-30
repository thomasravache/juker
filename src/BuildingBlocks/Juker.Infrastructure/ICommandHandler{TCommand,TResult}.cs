using Juker.SharedKernel;

namespace Juker.Infrastructure;

/// <summary>Handler de comando; resolvido do DI já envolvido pelos decorators (validação, logging, unidade de trabalho).</summary>
/// <typeparam name="TCommand">O comando.</typeparam>
/// <typeparam name="TResult">Tipo do valor do resultado.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken ct);
}
