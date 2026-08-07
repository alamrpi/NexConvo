using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NexConvo.Automation.Infrastructure;
using NexConvo.Automation.Infrastructure.Jobs;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using Serilog;

// Minimal standards-aligned host (skill Standards 9, 12, 6). Automation is a trigger-only
// scheduler: a Hangfire recurring job publishes CheckIntegrationHealthCommand to RabbitMQ and
// each owning service (Chat, Integrations, ...) re-tests its own configs. Automation holds no
// credentials and no per-tenant config DbContext.
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

builder.Services.AddAutomationInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
app.UseAuthorization();

app.UseNexConvoSwagger();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
});
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

var healthSweepCron = builder.Configuration["HealthSweep:Cron"] ?? "0 */6 * * *";
RecurringJob.AddOrUpdate<IntegrationHealthSweepScheduler>(
    "integration-health-sweep",
    job => job.Trigger(),
    healthSweepCron);

await app.RunAsync();
