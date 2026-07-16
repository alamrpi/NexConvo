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
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3003", "http://localhost:3007")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

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
        options.Events = new JwtBearerEvents
        {
            // The /hubs/{**catch-all} route enforces AuthorizationPolicy "default", but browsers
            // cannot set an Authorization header on a WebSocket handshake — SignalR clients send
            // the JWT as an access_token query parameter instead. Honor it for hub paths only;
            // YARP forwards the query string, and the Chat service re-authenticates with the same
            // hook. The token never reaches the access log: Serilog request logging records
            // RequestPath only, not the query string.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
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

    // Tighter per-IP limiter for the anonymous public widget (config fetch + hub connect). The
    // widget is unauthenticated and drives a tenant's paid LLM, so it needs a stricter cap than the
    // global policy (audit C4). Applied to the widget routes via "RateLimiterPolicy" in YARP config.
    options.AddPolicy("widget-public", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 30,
            TokensPerPeriod = 30,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDevCors");
}

app.UseNexConvoRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
app.UseAuthorization();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger-json/identity/swagger.json", "Identity API");
    c.SwaggerEndpoint("/swagger-json/corecrm/swagger.json", "Core CRM API");
    c.SwaggerEndpoint("/swagger-json/chat/swagger.json", "Chat API");
    c.SwaggerEndpoint("/swagger-json/voice/swagger.json", "Voice API");
    c.SwaggerEndpoint("/swagger-json/aiassistant/swagger.json", "AI Assistant API");
    c.SwaggerEndpoint("/swagger-json/integrations/swagger.json", "Integrations API");
    c.SwaggerEndpoint("/swagger-json/knowledge/swagger.json", "Knowledge API");
    c.SwaggerEndpoint("/swagger-json/automation/swagger.json", "Automation API");
    c.RoutePrefix = "swagger";
});

// Liveness = process is up; readiness = ready to route. Gateway has no own datastore.
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapReverseProxy();

await app.RunAsync();
