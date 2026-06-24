using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using NexConvo.BuildingBlocks.Infrastructure.Web;
using NexConvo.BuildingBlocks.Observability;
using NexConvo.Identity.Application;
using NexConvo.Identity.Infrastructure;
using NexConvo.Identity.Infrastructure.Persistence;
using NexConvo.Identity.Infrastructure.Security;
using Serilog;

const string serviceName = "identity";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNexConvoSerilog(serviceName);
builder.Services.AddNexConvoOpenTelemetry(builder.Configuration, serviceName);

builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Identity validates its OWN tokens with the in-memory RSA public key — no self-HTTP to JWKS.
// Other services validate via Jwt:Authority/JWKS (see gateway + service appsettings).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<RsaKeyProvider, IConfiguration>((options, keys, configuration) =>
    {
        options.RequireHttpsMetadata = false; // tokens validated by key, not HTTPS metadata
        options.MapInboundClaims = false;      // keep "sub"/"role" claim names as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "nexconvo-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keys.SecurityKey,
            ValidateLifetime = true,
            RoleClaimType = "role",
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Deny-by-default (skill Standard 12): everything requires auth unless [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // Permission policies map to the JWT 'permission' claims; '*' (Owner) satisfies any.
    static Action<AuthorizationPolicyBuilder> RequirePermission(string key) => policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("permission", "*") || context.User.HasClaim("permission", key));

    options.AddPolicy("settings:manage", RequirePermission("settings:manage"));
    options.AddPolicy("users:read", RequirePermission("users:read"));
    options.AddPolicy("users:invite", RequirePermission("users:invite"));
    options.AddPolicy("users:manage", RequirePermission("users:manage"));
    options.AddPolicy("roles:manage", RequirePermission("roles:manage"));
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Auto-apply pending migrations on startup (Development by default) using a privileged
// connection. No-op when the database is already up to date.
await IdentityDatabaseMigrator.MigrateAsync(builder.Configuration, builder.Environment, app.Logger);

app.UseNexConvoExceptionHandling();
app.UseSerilogRequestLogging();
app.UseRequestCorrelation();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

await app.RunAsync();

/// <summary>Exposed so the integration test host (WebApplicationFactory&lt;Program&gt;) can boot the app.</summary>
public partial class Program;
