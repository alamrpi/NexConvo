using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace NexConvo.BuildingBlocks.Infrastructure.Web;

public static class SwaggerExtensions
{
    public static IServiceCollection AddNexConvoSwagger(this IServiceCollection services, string apiName, string version = "v1")
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(version, new OpenApiInfo { Title = apiName, Version = version });

            // Define the OAuth2.0 scheme that's in use (i.e., Implicit Flow or Bearer Token).
            // For simple Gateway testing, a Bearer token is easiest.
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UseNexConvoSwagger(this WebApplication app)
    {
        app.UseSwagger();
        // UI is intentionally omitted here because it will be served centrally from the API Gateway.
        return app;
    }
}
