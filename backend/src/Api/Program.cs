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
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
}

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program { }
