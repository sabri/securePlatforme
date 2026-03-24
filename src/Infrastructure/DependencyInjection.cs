using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SecurePlatform.Application.Common;
using SecurePlatform.Application.Interfaces;
using SecurePlatform.Domain.Entities;
using SecurePlatform.Infrastructure.Data;
using SecurePlatform.Infrastructure.Services;
using StackExchange.Redis;

namespace SecurePlatform.Infrastructure;

/// <summary>
/// Clean Architecture pattern: each layer registers its own services.
/// Called from Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ═══════════════════════════════════════════════════════════
        // [SECURITY: SQL INJECTION] — Entity Framework Core with
        // parameterized queries. Connection strings are loaded from
        // configuration, never from user input. EF Core converts
        // all LINQ queries to parameterized SQL automatically.
        // ═══════════════════════════════════════════════════════════
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // ─── ASP.NET Core Identity ──────────────────────────────
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Password rules (relaxed for learning, tighten in production!)
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // ─── Redis ───────────────────────────────────────────────
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        // ─── JWT Settings ───────────────────────────────────────
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
            };

            options.Events = new JwtBearerEvents
            {
                // ═══════════════════════════════════════════════════
                // [SECURITY: HTTP-ONLY COOKIES + BFF PATTERN] — Read
                // the JWT from the HTTP-only "AccessToken" cookie
                // instead of the Authorization header. This ensures
                // tokens are never accessible to client-side JavaScript,
                // preventing XSS-based token theft. The browser sends
                // the cookie automatically on every same-origin request.
                // ═══════════════════════════════════════════════════
                OnMessageReceived = context =>
                {
                    // Prefer the cookie; fall back to Authorization header (for Swagger)
                    var cookieToken = context.Request.Cookies["AccessToken"];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                    return Task.CompletedTask;
                },

                // ═══════════════════════════════════════════════════
                // [SECURITY: TOKEN REVOCATION] — After the JWT is
                // structurally validated, check the Redis blacklist
                // for the token's JTI claim. Revoked tokens (from
                // logout or token rotation) are immediately rejected.
                // ═══════════════════════════════════════════════════
                OnTokenValidated = async context =>
                {
                    var revocationService = context.HttpContext.RequestServices
                        .GetRequiredService<ITokenRevocationService>();

                    var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (jti != null && await revocationService.IsTokenRevokedAsync(jti))
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            };
        });

        // ─── Services ───────────────────────────────────────────
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddSingleton<ITokenRevocationService, RedisTokenRevocationService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}
