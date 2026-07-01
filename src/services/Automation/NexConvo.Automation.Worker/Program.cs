using Microsoft.AspNetCore.Authentication.JwtBearer;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using Serilog;

// Minimal standards-aligned host (skill Standards 9, 12, 6). Endpoints, CQRS handlers,
// the DbContext + RLS interceptor, MassTransit, and per-endpoint RBAC are added during
// the per-service implementation pass.
const string serviceName = "automation";

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
builder.Services.AddNexConvoSwagger("Automation API");
builder.Services.AddHealthChecks();

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
