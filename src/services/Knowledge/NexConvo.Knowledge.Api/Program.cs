using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.Knowledge.Api.Grpc;
using NexConvo.Knowledge.Application;
using NexConvo.Knowledge.Infrastructure;
using NexConvo.Knowledge.Infrastructure.Persistence;
using Serilog;

const string serviceName = "knowledge";

var builder = WebApplication.CreateBuilder(args);

// gRPC clients connect via HTTP/2 prior-knowledge (no TLS ALPN, no HTTP/1.1 Upgrade handshake).
// Kestrel's mixed Http1AndHttp2 protocol sniffing on a single plaintext endpoint does not reliably
// detect a bare h2c preface the way it detects an Upgrade-based h2c request — confirmed by testing
// both the REST endpoints (h2c works via Upgrade) and gRPC (fails with HTTP_1_1_REQUIRED) on the
// same port. A dedicated Http2-only port for gRPC sidesteps that ambiguity entirely, which is the
// documented ASP.NET Core pattern for hosting REST + gRPC on the same Kestrel instance.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(8081, listenOptions =>
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Host.UseNexConvoSerilog(serviceName);
builder.Services.AddNexConvoOpenTelemetry(builder.Configuration, serviceName);
builder.Services.AddNexConvoTenancy();

// The gRPC retrieval path has no HttpContext, so the tenant travels on GrpcCallContext instead
// of the JWT claim. CompositeTenantContext replaces the HttpTenantContext registration
// AddNexConvoTenancy just added — one ITenantContext resolves correctly for both call kinds, so
// every existing handler/repository/DbContext interceptor keeps injecting ITenantContext unchanged.
builder.Services.AddScoped<GrpcCallContext>();
builder.Services.Replace(ServiceDescriptor.Scoped<ITenantContext, CompositeTenantContext>());

builder.Services.AddKnowledgeApplication();
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
// Slice 1 embedding providers (BGE-M3 / Cohere) — the Knowledge service is the embedder.
builder.Services.AddEmbeddingProviders(builder.Configuration);

// First gRPC contract in the repo (docs/ARCHITECTURE.md §8) — internal-only retrieval seam shared
// by Chat/Voice. Not routed through the public YARP gateway (skill Standard 21 doesn't apply:
// this is deliberately NOT a gateway-exposed route). Authenticated by a shared key, not a JWT
// policy (skill Standard 12) — see InternalServiceAuthInterceptor for the written reason.
builder.Services.AddGrpc(options => options.Interceptors.Add<InternalServiceAuthInterceptor>());

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
    options.AddPolicy("knowledge:manage", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("permission", "*") || ctx.User.HasClaim("permission", "knowledge:manage")));
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddNexConvoSwagger("Knowledge API");
builder.Services.AddHealthChecks()
    .AddEmbeddingProviderHealthCheck();

var app = builder.Build();

await KnowledgeDatabaseMigrator.MigrateAsync(builder.Configuration, builder.Environment, app.Logger);

app.UseNexConvoExceptionHandling();
// Swagger JSON is served before auth so the deny-by-default FallbackPolicy doesn't 401 it
// (the gateway aggregates the UI). Mirrors the Identity service ordering.
app.UseNexConvoSwagger();
app.UseNexConvoRequestLogging();
app.UseAuthentication();
app.UseRequestCorrelation();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Dev-only job dashboard; anonymous access is acceptable only because it never ships to prod.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
    });
}

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Internal-only: exempted from the HTTP JWT FallbackPolicy (no JWT exists between services) —
// InternalServiceAuthInterceptor is this endpoint's actual AuthN gate (skill Standard 12).
// Never routed through YARP; only reachable service-to-service inside the cluster network.
// Real Kestrel listens on both 8080 (Http1) and 8081 (Http2-only, see ConfigureKestrel above);
// WebApplicationFactory's in-memory TestServer has no real ports, so RequireHost would wrongly
// reject test requests — only constrain the host when a real Kestrel server is listening.
var grpcEndpoint = app.MapGrpcService<KnowledgeRetrievalGrpcService>().AllowAnonymous();
if (app.Services.GetService<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>() is not null)
{
    grpcEndpoint.RequireHost("*:8081");
}

await app.RunAsync();

public partial class Program;
