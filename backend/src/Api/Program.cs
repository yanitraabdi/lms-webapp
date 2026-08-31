using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Academy.Api;
using Academy.Api.Endpoints;
using Academy.Application.Auth;
using Academy.Infrastructure;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Engagement;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Programs;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.Extensions.Options;
var builder = WebApplication.CreateBuilder(args);

// RFC-7807 problem details (+ handler that maps AuthException / validation errors).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuthExceptionHandler>();

// OpenAPI document — single source of truth for the generated frontend client.
builder.Services.AddOpenApi();

// Serialize enums as strings in JSON (e.g. module access "Preview"/"Locked"/"Entitled").
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Health checks (liveness).
builder.Services.AddHealthChecks();

// CORS for the Next.js frontend origin(s).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Rate limiter: per-client-IP fixed window for auth/payment/playback (auth → /api/auth/*).
// PermitLimit is configurable (raised in tests) so it throttles real clients, not the whole app.
// The API never faces the internet directly: the browser reaches it through the Next
// same-origin proxy (and, in the tunnel deploy, Cloudflare before that). Without this,
// Connection.RemoteIpAddress is the FRONTEND CONTAINER's address for every request, so all
// per-IP rate limits collapse into one bucket shared by every user — brute-force protection
// on /api/auth becomes meaningless and one cohort starting an exam together can exhaust the
// media limit for everyone.
//
// ForwardLimit = 1 takes only the rightmost X-Forwarded-For entry. Cloudflare APPENDS the true
// client IP to whatever the caller sent, so the rightmost entry is the one it vouches for and a
// spoofed header cannot displace it. KnownNetworks is restricted to private ranges so a request
// arriving from a public address cannot present itself as a trusted proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));
});

var permitLimit = builder.Configuration.GetValue<int?>("RateLimits:PermitLimit") ?? 20;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var policy in new[] { "auth", "payment", "playback" })
        options.AddPolicy(policy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

    // Media streaming gets its own, wider policy: a single <audio> element issues several range
    // requests per play (seek = new request), so the 20/min "playback" budget (sized for one
    // fetch per page view) would 429 mid-playback. The signature already authorizes *what* can be
    // requested and the TTL bounds *for how long*, so this limiter isn't authenticating — it's
    // only there to cap bulk enumeration of the key space. 300/min (5/sec) comfortably covers
    // normal seeking while still bounding a scripted sweep.
    // NOTE: partitioned on RemoteIpAddress, same as the policies above. Behind the tunnel
    // topology the API only ever sees the frontend container's IP (no UseForwardedHeaders is
    // configured), so in that deployment this budget is shared by every concurrent listener
    // rather than per-student. Tracked as a follow-up; see the task report.
    options.AddPolicy("media", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(permitLimit, 300),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// FluentValidation validators (from the Application assembly).
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequest>();

// AuthN (JWT bearer) + AuthZ policies.
var authOptions = AuthOptionsFactory.Build(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep 'sub' / 'email_verified' claim names as-is
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authOptions.JwtIssuer,
            ValidAudience = authOptions.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSigningKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmailVerified", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("email_verified", "true"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin", "SuperAdmin"));
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
});

// Data layer + auth services + provider abstractions.
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// A localhost FrontendBaseUrl in Production means every verification link, reset link and
// certificate verify URL is unreachable. Certificates are immutable, so a bad URL cannot be
// corrected after issue — fail at startup instead of discovering it from a learner.
if (app.Environment.IsProduction())
{
    var frontendBase = app.Services.GetRequiredService<IOptions<AuthOptions>>().Value.FrontendBaseUrl;
    if (frontendBase.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"FrontendBaseUrl is '{frontendBase}' in Production. Set FrontendBaseUrl (or " +
            "Cors__AllowedOrigins__0) to the public origin, e.g. https://your-domain.");
}

// Must run before UseRateLimiter: the limiter partitions on RemoteIpAddress.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapSubscriptionEndpoints();
app.MapWebhookEndpoints();
app.MapLearningEndpoints();
app.MapAdminEndpoints();
app.MapCurriculumAdminEndpoints();
app.MapUserAdminEndpoints();
app.MapAdminQuizEndpoints();
app.MapEngagementEndpoints();
app.MapNotificationEndpoints();
app.MapLearnerEngagementEndpoints();
// INVERTA (M2)
app.MapProgramEndpoints();
app.MapProgramAdminEndpoints();
app.MapSessionEndpoints();
app.MapAssessmentAdminEndpoints();
app.MapFinalAssessmentEndpoints();
app.MapMediaEndpoints();

app.MapAdminOperationsEndpoints();
// Dev-only payment simulation endpoints (active when Billing:Provider = "dev").
if (app.Services.GetRequiredService<BillingOptions>().IsDev)
    app.MapDevPaymentEndpoints();

// Apply EF migrations on startup only when explicitly enabled (docker-compose api service).
// Integration tests (WebApplicationFactory) leave this off, so /health needs no database.
if (app.Configuration.GetValue<bool>("RunMigrations"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Seed a sample curriculum (idempotent) when enabled (docker-compose dev).
if (app.Configuration.GetValue<bool>("SeedSampleData"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PlansSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<DevAdminSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<FaqSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<ProgramSeeder>().SeedAsync();
    // Runnable sample test on session 1 — depends on ProgramSeeder having created the program.
    await scope.ServiceProvider.GetRequiredService<SampleTestSeeder>().SeedAsync();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program { }
