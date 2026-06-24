using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NexConvo.BuildingBlocks.Observability;
using Serilog;

const string serviceName = "gateway";

var builder = WebApplication.CreateBuilder(args);

// Observability: structured, trace-correlated logs + OpenTelemetry (skill Standard 9).
builder.Host.UseNexConvoSerilog(serviceName);
builder.Services.AddNexConvoOpenTelemetry(builder.Configuration, serviceName);

// YARP reverse proxy — routes/clusters from configuration.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT validation at the edge (skill Standard 12 — services still enforce per-endpoint RBAC).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Jwt").Bind(options);
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization();

// Rate limiting partitioned per tenant, falling back to IP (OPERATIONS.md §4).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("tenant_id")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseNexConvoRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
app.UseAuthorization();

// Liveness = process is up; readiness = ready to route. Gateway has no own datastore.
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapReverseProxy();

await app.RunAsync();
