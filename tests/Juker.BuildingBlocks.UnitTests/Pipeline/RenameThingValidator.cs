using FluentValidation;

namespace Juker.BuildingBlocks.UnitTests.Pipeline;

/// <summary>Validator do comando de teste, registrado por <c>AddModuleHandlers</c> (scan do assembly).</summary>
public sealed class RenameThingValidator : AbstractValidator<RenameThing>
{
    public RenameThingValidator() => RuleFor(command => command.Name).NotEmpty();
}
