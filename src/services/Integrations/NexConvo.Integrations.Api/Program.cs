using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.Integrations.Application;
using NexConvo.Integrations.Infrastructure;
using NexConvo.Integrations.Infrastructure.Persistence;
using Serilog;

const string serviceName = "integrations";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNexConvoSerilog(serviceName);
builder.Services.AddNexConvoOpenTelemetry(builder.Configuration, serviceName);
builder.Services.AddNexConvoTenancy();

builder.Services.AddIntegrationsApplication();
builder.Services.AddIntegrationsInfrastructure(builder.Configuration);
builder.Services.AddAiProviders();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Jwt").Bind(options);
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false // Trust gateway JWT configuration
        };
    });
builder.Services.AddAuthorization(options =>
{
    // Deny-by-default (skill Standard 12): every endpoint requires auth unless [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // Permission policies map to the JWT 'permission' claims; '*' (Owner) satisfies any.
    options.AddPolicy("settings:manage", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("permission", "*") || ctx.User.HasClaim("permission", "settings:manage")));
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddNexConvoSwagger("Integrations API");
builder.Services.AddHealthChecks();

var app = builder.Build();

await IntegrationsDatabaseMigrator.MigrateAsync(builder.Configuration, builder.Environment, app.Logger);

app.UseNexConvoExceptionHandling();
// Swagger JSON is served before auth so the deny-by-default FallbackPolicy doesn't 401 it
// (the gateway aggregates the UI). Mirrors the Identity service ordering.
app.UseNexConvoSwagger();
app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

await app.RunAsync();

public partial class Program;
