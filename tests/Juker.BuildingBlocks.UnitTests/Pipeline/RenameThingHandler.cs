using Juker.Infrastructure;
using Juker.SharedKernel;

namespace Juker.BuildingBlocks.UnitTests.Pipeline;

/// <summary>Handler de teste do UT-02: só registra que foi chamado.</summary>
public sealed class RenameThingHandler(HandlerCalls calls) : ICommandHandler<RenameThing, Guid>
{
    public static readonly Guid RenamedId = new("0199a0c0-0000-7000-8000-000000000001");

    public Task<Result<Guid>> Handle(RenameThing command, CancellationToken ct)
    {
        calls.Record(command);
        return Task.FromResult(Result<Guid>.Success(RenamedId));
    }
}
