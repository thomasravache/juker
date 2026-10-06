using FluentValidation;
using FluentValidation.Results;
using Juker.SharedKernel;

namespace Juker.Infrastructure.Pipeline;

internal sealed class ValidationQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (validators.Any())
        {
            var failures = new List<ValidationFailure>();
            foreach (var validator in validators)
            {
                var validationResult = await validator.ValidateAsync(query, ct).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    failures.AddRange(validationResult.Errors);
                }
            }

            if (failures.Count > 0)
            {
                var details = failures
                    .GroupBy(f => ToCamelCase(f.PropertyName), StringComparer.Ordinal)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => f.ErrorMessage).ToArray(),
                        StringComparer.Ordinal);

                var error = new Error("validation", "Falha de validação da consulta.", ErrorType.Validation)
                {
                    Details = details,
                };

                return Result<TResult>.Failure(error);
            }
        }

        return await inner.Handle(query, ct).ConfigureAwait(false);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name)
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
