using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurePlatform.Application.DTOs.Auth;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.API.Controllers;

// ═══════════════════════════════════════════════════════════════
// [BFF PATTERN] — This controller sits behind /api/auth/* and
// acts as the Backend-For-Frontend gateway. The React SPA calls
// these endpoints; tokens are returned in HTTP-only cookies —
// never exposed to JavaScript — eliminating token theft via XSS.
// ═══════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAntiforgery _antiforgery;

    public AuthController(IAuthService authService, IAntiforgery antiforgery)
    {
        _authService = authService;
        _antiforgery = antiforgery;
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: CSRF / XSRF] — The frontend must call this GET
    // endpoint first to receive a CSRF token cookie. All subsequent
    // POST/PUT/DELETE requests must include the X-XSRF-TOKEN header
    // with the value from this cookie.
    // ═══════════════════════════════════════════════════════════
    /// <summary>
    /// Issue an anti-forgery token cookie so the SPA can send it
    /// on mutating requests.
    /// GET /api/auth/csrf-token
    /// </summary>
    [HttpGet("csrf-token")]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,   // JS must read this value
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict
            });
        return Ok(new { message = "CSRF token issued." });
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: RATE LIMITING] — The "auth" rate-limit policy
    // (5 requests / 60 seconds per IP) is applied to register,
    // login, forgot-password, and reset-password to prevent
    // brute-force and credential-stuffing attacks.
    // ═══════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: SQL INJECTION] — All inputs flow through
    // ASP.NET Core model binding → Entity Framework Core
    // parameterized queries. No raw SQL is ever concatenated,
    // so SQL injection is structurally prevented.
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Register a new user account.
    /// POST /api/auth/register
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // ═══════════════════════════════════════════════════════
        // [SECURITY: CSRF / XSRF] — Validate anti-forgery token
        // on every mutating (POST) request to prevent cross-site
        // request forgery attacks when using cookie auth.
        // ═══════════════════════════════════════════════════════
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        // ═══════════════════════════════════════════════════════
        // [SECURITY: XSS] — Input validation: reject HTML/script
        // tags in user-supplied text fields to block stored XSS.
        // ═══════════════════════════════════════════════════════
        if (ContainsHtmlOrScript(request.Email) ||
            ContainsHtmlOrScript(request.FirstName) ||
            ContainsHtmlOrScript(request.LastName))
        {
            return BadRequest(new { message = "Input contains invalid characters." });
        }

        var result = await _authService.RegisterAsync(request);
        if (!result.Succeeded)
            return Ok(result); // 200 with EmailNotConfirmed — tells client to show confirmation page

        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);
        return Ok(AuthResponseWithoutTokens(result));
    }

    /// <summary>
    /// Confirm email with the token sent to the user's inbox.
    /// POST /api/auth/confirm-email
    /// </summary>
    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        var result = await _authService.ConfirmEmailAsync(request.Email, request.Token);
        if (!result.Succeeded)
            return BadRequest(result);

        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);
        return Ok(AuthResponseWithoutTokens(result));
    }

    /// <summary>
    /// Resend the email confirmation token.
    /// POST /api/auth/resend-confirmation
    /// </summary>
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ForgotPasswordRequest request)
    {
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        var result = await _authService.ResendConfirmationEmailAsync(request.Email);
        return Ok(result);
    }

    /// <summary>
    /// Login with email & password → tokens set in HTTP-only cookies.
    /// POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // [SECURITY: CSRF / XSRF] — Validate anti-forgery token
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        var result = await _authService.LoginAsync(request);
        if (!result.Succeeded)
            return Unauthorized(result);

        // [SECURITY: HTTP-ONLY COOKIES] — Deliver tokens via cookies, not JSON body
        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);

        return Ok(AuthResponseWithoutTokens(result));
    }

    /// <summary>
    /// Refresh an expired access token using the refresh token cookie.
    /// POST /api/auth/refresh
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RefreshToken()
    {
        // ═══════════════════════════════════════════════════════
        // [SECURITY: HTTP-ONLY COOKIES + BFF] — Read tokens from
        // cookies instead of the request body. The browser auto-
        // sends cookies; the SPA never touches the raw JWT.
        // ═══════════════════════════════════════════════════════
        var accessToken = Request.Cookies["AccessToken"];
        var refreshToken = Request.Cookies["RefreshToken"];

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "No auth cookies found." });

        var refreshRequest = new RefreshTokenRequest(accessToken, refreshToken);
        var result = await _authService.RefreshTokenAsync(refreshRequest);
        if (!result.Succeeded)
        {
            ClearAuthCookies();
            return Unauthorized(result);
        }

        // [SECURITY: HTTP-ONLY COOKIES] — Rotate cookies with new tokens
        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);

        return Ok(AuthResponseWithoutTokens(result));
    }

    /// <summary>
    /// Logout — revokes tokens and clears auth cookies.
    /// POST /api/auth/logout
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        await _authService.LogoutAsync(userId, jti);

        // ═══════════════════════════════════════════════════════
        // [SECURITY: HTTP-ONLY COOKIES] — Clear auth cookies on
        // logout so no stale tokens remain in the browser.
        // ═══════════════════════════════════════════════════════
        ClearAuthCookies();

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Get the current authenticated user's profile.
    /// GET /api/auth/me
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _authService.GetCurrentUserAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    /// <summary>
    /// Request a password reset code sent to email.
    /// POST /api/auth/forgot-password
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // [SECURITY: CSRF / XSRF] — Validate anti-forgery token
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        var result = await _authService.ForgotPasswordAsync(request);
        // Always return 200 to avoid email enumeration attacks
        return Ok(result);
    }

    /// <summary>
    /// Reset password using the code received via email.
    /// POST /api/auth/reset-password
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // [SECURITY: CSRF / XSRF] — Validate anti-forgery token
        try { await _antiforgery.ValidateRequestAsync(HttpContext); }
        catch { return BadRequest(new { message = "Invalid CSRF token." }); }

        var result = await _authService.ResetPasswordAsync(request);
        if (result.ResultType == SecurePlatform.Domain.Enums.AuthResultType.PasswordResetFailed)
            return BadRequest(result);

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    // Private Helpers
    // ═══════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: HTTP-ONLY COOKIES] — Helper to set auth tokens
    // as HTTP-only, Secure, SameSite=Strict cookies. This is the
    // core BFF defence: tokens never appear in JS-accessible
    // storage (no localStorage, no sessionStorage).
    // ═══════════════════════════════════════════════════════════
    private void SetAuthCookies(string accessToken, string refreshToken, DateTime expiresAt)
    {
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,        // Not accessible to JavaScript (XSS defence)
            Secure = true,          // Only sent over HTTPS
            SameSite = SameSiteMode.Strict, // Not sent on cross-site requests (CSRF defence)
            Expires = expiresAt,
            Path = "/"
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/api/auth"      // Only sent to auth endpoints (minimize exposure)
        };

        Response.Cookies.Append("AccessToken", accessToken, accessCookieOptions);
        Response.Cookies.Append("RefreshToken", refreshToken, refreshCookieOptions);
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: HTTP-ONLY COOKIES] — Remove auth cookies on
    // logout or refresh failure to prevent stale token reuse.
    // ═══════════════════════════════════════════════════════════
    private void ClearAuthCookies()
    {
        Response.Cookies.Delete("AccessToken", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("RefreshToken", new CookieOptions { Path = "/api/auth" });
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: XSS] — Strip tokens from the response body so
    // they are only delivered via HTTP-only cookies. Even if XSS
    // runs, it cannot read the response and steal tokens.
    // ═══════════════════════════════════════════════════════════
    private static AuthResponse AuthResponseWithoutTokens(AuthResponse response)
    {
        return new AuthResponse
        {
            Succeeded = response.Succeeded,
            ResultType = response.ResultType,
            AccessToken = null,       // Not sent in JSON — only in cookie
            RefreshToken = null,      // Not sent in JSON — only in cookie
            ExpiresAt = response.ExpiresAt,
            Message = response.Message,
            User = response.User
        };
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: XSS] — Reject inputs containing HTML tags or
    // script content. This is a defence-in-depth measure on top
    // of output encoding already done by React and ASP.NET Core.
    // ═══════════════════════════════════════════════════════════
    private static bool ContainsHtmlOrScript(string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(input,
            @"<[^>]*(script|iframe|object|embed|form|input|img|svg|on\w+\s*=)[^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
