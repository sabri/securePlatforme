using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using SecurePlatform.Infrastructure;
using SecurePlatform.AI;

var builder = WebApplication.CreateBuilder(args);

// ─── Layer Registration (Clean Architecture) ─────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAiServices(builder.Configuration);
builder.Services.AddHttpClient();

// ─── API Services ────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "SecurePlatform API", Version = "v1" });

    // Add JWT auth to Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ═══════════════════════════════════════════════════════════════
// [SECURITY: CORS] — Restrict cross-origin requests to the
// trusted React frontend only. AllowCredentials is required so
// the browser sends HTTP-only cookies with every request.
// ═══════════════════════════════════════════════════════════════
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174") // SecurePlatform + IntelliLog clients
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for cookies
    });
});

// ═══════════════════════════════════════════════════════════════
// [SECURITY: RATE LIMITING] — Protect against brute-force login
// attempts, credential stuffing, and API abuse. Three policies:
//   • "auth"   → strict 5 req / 60s per IP (login, register)
//   • "global" → general 100 req / 60s per IP
//   • "ai"     → 10 req / 60s per IP (expensive AI calls)
// ═══════════════════════════════════════════════════════════════
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Strict limiter for auth endpoints (login, register, forgot-password)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromSeconds(60);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // General limiter for all other endpoints
    options.AddFixedWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromSeconds(60);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // Strict limiter for AI endpoints (expensive operations)
    options.AddFixedWindowLimiter("ai", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromSeconds(60);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// ═══════════════════════════════════════════════════════════════
// [SECURITY: CSRF / XSRF] — Anti-forgery protection is required
// because we use HTTP-only cookies for auth. Without this, a
// malicious site could forge requests using the victim's cookies.
// The server issues a CSRF token cookie; the client reads it and
// sends it back in the X-XSRF-TOKEN header on every mutating
// request (POST/PUT/DELETE).
// ═══════════════════════════════════════════════════════════════
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";       // Client sends token in this header
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

// ─── Seed Database ──────────────────────────────────────────
await DbInitializer.SeedAsync(app.Services);

// ─── Middleware Pipeline ────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ═══════════════════════════════════════════════════════════════
// [SECURITY: XSS + CLICKJACKING + SNIFFING] — HTTP security
// headers that instruct browsers to block common attack vectors:
//   • X-Content-Type-Options: nosniff  → prevents MIME-sniffing
//   • X-Frame-Options: DENY            → prevents clickjacking
//   • X-XSS-Protection: 1; mode=block  → legacy XSS filter
//   • Referrer-Policy: strict-origin    → limits referrer leakage
//   • Content-Security-Policy           → restricts resource loading
//   • Permissions-Policy                → disables unused browser APIs
// ═══════════════════════════════════════════════════════════════
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none';";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseCors("AllowReactApp");

// ═══════════════════════════════════════════════════════════════
// [SECURITY: RATE LIMITING] — Apply rate limiting middleware
// before authentication so abusive requests are rejected early.
// ═══════════════════════════════════════════════════════════════
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ═══════════════════════════════════════════════════════════════
// [SECURITY: CSRF / XSRF] — Issue the CSRF token cookie on
// every GET request so the React frontend can read it and include
// it as a header on subsequent mutating requests.
// ═══════════════════════════════════════════════════════════════
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,   // JS must read this value
                Secure = !app.Environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict
            });
    }
    await next();
});

// ═══════════════════════════════════════════════════════════════
// [BFF PATTERN] — The /bff/* endpoints proxy React's API calls
// through the backend. This keeps the API surface hidden from
// the public internet and allows HTTP-only cookie auth without
// exposing tokens to JavaScript.
// ═══════════════════════════════════════════════════════════════
app.MapControllers();

app.Run();
