using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Behaviors;
using NexConvo.Identity.Application.Behaviors;

namespace NexConvo.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(VerifiedActorBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
