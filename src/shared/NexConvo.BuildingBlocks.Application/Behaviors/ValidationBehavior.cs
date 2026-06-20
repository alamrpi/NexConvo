using FluentValidation;
using MediatR;

namespace NexConvo.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs all FluentValidation validators for a request before
/// the handler. On failure it throws <see cref="ValidationException"/>, mapped to 422 at the
/// API edge. Every write command gets validated without the handler invoking validators.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators as IValidator<TRequest>[] ?? [.. validators];
        if (validatorList.Length != 0)
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(
                validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
