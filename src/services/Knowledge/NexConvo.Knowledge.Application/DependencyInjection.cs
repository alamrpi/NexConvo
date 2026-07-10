using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Behaviors;
using System.Reflection;

namespace NexConvo.Knowledge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Deny invalid commands before they reach a handler (skill Standard 3): the behavior
            // throws FluentValidation.ValidationException → 422 via UseNexConvoExceptionHandling.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
