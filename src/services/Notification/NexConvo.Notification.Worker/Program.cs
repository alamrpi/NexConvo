using Microsoft.AspNetCore.Authentication.JwtBearer;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.Notification.Infrastructure;
using Serilog;

// Minimal standards-aligned host (skill Standards 9, 12, 6). Notification is event-driven, not
// scheduled — it holds no Hangfire, no per-tenant config DbContext, and no public business API;
// it only consumes IntegrationHealthFailedEvent and calls Identity's internal endpoint.
const string serviceName = "notification";

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
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddNexConvoSwagger("Notification API");
builder.Services.AddHealthChecks();

builder.Services.AddNotificationInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
app.UseAuthorization();

app.UseNexConvoSwagger();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

await app.RunAsync();
