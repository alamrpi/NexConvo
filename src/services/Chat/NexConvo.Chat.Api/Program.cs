using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.Chat.Application;
using NexConvo.Chat.Infrastructure;
using NexConvo.Chat.Infrastructure.Persistence;
using Serilog;

// Minimal standards-aligned host (skill Standards 9, 12, 6). Endpoints, CQRS handlers,
// the DbContext + RLS interceptor, MassTransit, and per-endpoint RBAC are added during
// the per-service implementation pass.
const string serviceName = "chat";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNexConvoSerilog(serviceName);
builder.Services.AddNexConvoOpenTelemetry(builder.Configuration, serviceName);
builder.Services.AddNexConvoTenancy();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Jwt").Bind(options);
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
// Permission policies: 'permission: *' (Owner wildcard) satisfies any specific key.
static Action<AuthorizationPolicyBuilder> RequirePermission(string key) => policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("permission", "*") || ctx.User.HasClaim("permission", key));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("settings:manage",   RequirePermission("settings:manage"));
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddNexConvoSwagger("Chat API");
builder.Services.AddHealthChecks();

builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
app.UseAuthorization();

app.UseNexConvoSwagger();
app.UseNexConvoExceptionHandling();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

await ChatDatabaseMigrator.MigrateAsync(builder.Configuration, builder.Environment, app.Logger);

await app.RunAsync();
