using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Behaviors;
using NexConvo.Chat.Application.Rag;

namespace NexConvo.Chat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddChatApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<IGroundingGate, GroundingGate>();
        services.AddSingleton<IAbstentionStreamFilter, AbstentionStreamFilter>();

        return services;
    }
}
