using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.Chat.Api.Extensions;
using NexConvo.Chat.Api.Realtime;
using NexConvo.Chat.Application;
using NexConvo.Chat.Infrastructure;
using NexConvo.Chat.Infrastructure.Persistence;
using Serilog;

// Minimal standards-aligned host (skill Standards 9, 12, 6). Endpoints, CQRS handlers,
// the DbContext + RLS interceptor, MassTransit, and per-endpoint RBAC are added during
// the per-service implementation pass.
const string serviceName = "chat";

// The Knowledge gRPC client connects over plaintext HTTP (no TLS in this deployment); .NET's
// HttpClient otherwise refuses to negotiate HTTP/2 without encryption and the call fails with
// "HTTP_1_1_REQUIRED". Must be set before any HttpClient/gRPC channel is constructed.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

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
        options.Events = new JwtBearerEvents
        {
            // Browsers cannot set an Authorization header on a WebSocket handshake; SignalR's
            // documented pattern is the access_token query parameter, honored ONLY for hub paths
            // so regular API calls keep header-only auth.
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
// Permission policies: 'permission: *' (Owner wildcard) satisfies any specific key.
static Action<AuthorizationPolicyBuilder> RequirePermission(string key) => policy =>
    policy.RequireAssertion(ctx =>
        ctx.User.HasClaim("permission", "*") || ctx.User.HasClaim("permission", key));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("settings:manage",     RequirePermission("settings:manage"));
    options.AddPolicy("conversations:read",  RequirePermission("conversations:read"));
    options.AddPolicy("conversations:write", RequirePermission("conversations:write"));
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddNexConvoSwagger("Chat API");
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WidgetCorsPolicy", policy =>
    {
        // The public widget is embedded on arbitrary tenant sites, so any origin may load it — but it
        // is anonymous and cookie-less (identity is the WidgetToken in the connection, not a cookie),
        // so credentials are NOT allowed. A credentialed wildcard would be a CSRF/credential-leak
        // anti-pattern (audit C4); reflecting the origin without credentials is the safe form.
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true);
    });
});
builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(builder.Configuration);
builder.Services.AddChatRealtime(builder.Configuration);
builder.Services.AddAiProviders();

var app = builder.Build();

app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation(); // after auth so tenant/user claims enrich the logs
// CORS must run before authorization and the endpoints — the widget controller/hubs declare a CORS
// policy via [EnableCors]/RequireCors, and without this middleware ASP.NET throws on every such
// request ("contains CORS metadata, but a middleware was not found that supports CORS").
app.UseCors();
app.UseAuthorization();

app.UseNexConvoSwagger();
app.UseNexConvoExceptionHandling();
app.MapControllers();
// Deny-by-default at the endpoint too (defense in depth on top of the hub's [Authorize]).
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization().RequireCors("WidgetCorsPolicy");
app.MapHub<PlaygroundHub>("/hubs/playground").RequireAuthorization();
// Public widget hub — no JWT required; the tenant is resolved from the unguessable ?token= param
// (WidgetToken) inside the hub, and RLS is enforced via the tenant-scoped context factory.
app.MapHub<WidgetHub>("/hubs/widget").AllowAnonymous().RequireCors("WidgetCorsPolicy");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

await ChatDatabaseMigrator.MigrateAsync(builder.Configuration, builder.Environment, app.Logger);

await app.RunAsync();

// Exposes the top-level-statement entry point to WebApplicationFactory<Program> for integration tests.
public partial class Program;
