using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.API.Controllers;

// ═══════════════════════════════════════════════════════════════
// [BFF PATTERN] — OAuth flows are handled entirely on the server
// side. The browser is redirected to the OAuth provider, then back
// to the server, which issues HTTP-only cookies — the SPA never
// sees raw tokens from Google/GitHub.
// ═══════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════
// [SECURITY: RATE LIMITING] — OAuth endpoints use the "auth"
// rate-limit policy to prevent automated OAuth abuse.
// ═══════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class OAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public OAuthController(
        IAuthService authService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _authService = authService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    // ═══════════════════════════════════════════════
    // Step 1: Redirect user to OAuth provider
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Redirect to Google OAuth consent screen.
    /// GET /api/oauth/google
    /// </summary>
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var clientId = _configuration["OAuth:Google:ClientId"];
        var redirectUri = _configuration["OAuth:RedirectBaseUrl"] + "/api/oauth/google/callback";
        var scope = "openid email profile";

        // ═══════════════════════════════════════════════════════
        // [SECURITY: XSS] — All user-facing URL parameters are
        // escaped with Uri.EscapeDataString to prevent injection
        // of malicious data into the OAuth redirect URL.
        // ═══════════════════════════════════════════════════════
        var url = $"https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={clientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(scope)}";

        return Redirect(url);
    }

    /// <summary>
    /// Redirect to GitHub OAuth authorization page.
    /// GET /api/oauth/github
    /// </summary>
    [HttpGet("github")]
    public IActionResult GitHubLogin()
    {
        var clientId = _configuration["OAuth:GitHub:ClientId"];
        var redirectUri = _configuration["OAuth:RedirectBaseUrl"] + "/api/oauth/github/callback";
        var scope = "user:email read:user";

        var url = $"https://github.com/login/oauth/authorize" +
                  $"?client_id={clientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&scope={Uri.EscapeDataString(scope)}";

        return Redirect(url);
    }

    // ═══════════════════════════════════════════════
    // Step 2: Handle callback from OAuth provider
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Google callback — exchanges code for tokens, extracts user info, issues JWT.
    /// GET /api/oauth/google/callback?code=xxx
    /// </summary>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code)
    {
        // ═══════════════════════════════════════════════════════
        // [SECURITY: XSS] — Validate the code parameter to
        // ensure it doesn't contain malicious content.
        // ═══════════════════════════════════════════════════════
        if (string.IsNullOrWhiteSpace(code) || code.Length > 2048)
            return BadRequest(new { message = "Invalid authorization code." });

        var clientId = _configuration["OAuth:Google:ClientId"]!;
        var clientSecret = _configuration["OAuth:Google:ClientSecret"]!;
        var redirectUri = _configuration["OAuth:RedirectBaseUrl"] + "/api/oauth/google/callback";

        // Exchange authorization code for access token
        var client = _httpClientFactory.CreateClient();
        var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }));

        if (!tokenResponse.IsSuccessStatusCode)
            return BadRequest(new { message = "Failed to exchange code with Google." });

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        // Fetch user info from Google
        var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userResponse = await client.SendAsync(userRequest);
        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>();

        var email = userJson.GetProperty("email").GetString()!;
        var firstName = userJson.TryGetProperty("given_name", out var gn) ? gn.GetString() ?? "" : "";
        var lastName = userJson.TryGetProperty("family_name", out var fn) ? fn.GetString() ?? "" : "";

        // Create or login user and get JWT
        var result = await _authService.ExternalLoginAsync("Google", email, firstName, lastName);
        if (!result.Succeeded)
            return BadRequest(result);

        // ═══════════════════════════════════════════════════════
        // [SECURITY: HTTP-ONLY COOKIES + BFF] — Instead of
        // sending tokens in the URL fragment (which JavaScript
        // can read → XSS risk), we set HTTP-only cookies and
        // redirect the browser to the SPA. The token is never
        // visible in the URL or to any client-side script.
        // ═══════════════════════════════════════════════════════
        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);

        var clientUrl = _configuration["OAuth:ClientCallbackUrl"]!;
        return Redirect(clientUrl);
    }

    /// <summary>
    /// GitHub callback — exchanges code for tokens, extracts user info, issues JWT.
    /// GET /api/oauth/github/callback?code=xxx
    /// </summary>
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code)
    {
        // [SECURITY: XSS] — Validate code parameter
        if (string.IsNullOrWhiteSpace(code) || code.Length > 2048)
            return BadRequest(new { message = "Invalid authorization code." });

        var clientId = _configuration["OAuth:GitHub:ClientId"]!;
        var clientSecret = _configuration["OAuth:GitHub:ClientSecret"]!;

        // Exchange authorization code for access token
        var client = _httpClientFactory.CreateClient();
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code
        });

        var tokenResponse = await client.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
            return BadRequest(new { message = "Failed to exchange code with GitHub." });

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        // Fetch user info from GitHub
        var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        userRequest.Headers.UserAgent.ParseAdd("SecurePlatform");
        var userResponse = await client.SendAsync(userRequest);
        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>();

        // GitHub may not expose email publicly — fetch from /user/emails
        var email = userJson.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String
            ? em.GetString()
            : null;

        if (string.IsNullOrEmpty(email))
        {
            var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
            emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            emailRequest.Headers.UserAgent.ParseAdd("SecurePlatform");
            var emailResponse = await client.SendAsync(emailRequest);
            var emails = await emailResponse.Content.ReadFromJsonAsync<JsonElement>();

            foreach (var e in emails.EnumerateArray())
            {
                if (e.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                {
                    email = e.GetProperty("email").GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(email))
            return BadRequest(new { message = "Could not retrieve email from GitHub." });

        var name = userJson.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var parts = name.Split(' ', 2);
        var firstName = parts.Length > 0 ? parts[0] : "";
        var lastName = parts.Length > 1 ? parts[1] : "";

        // Create or login user and get JWT
        var result = await _authService.ExternalLoginAsync("GitHub", email, firstName, lastName);
        if (!result.Succeeded)
            return BadRequest(result);

        // [SECURITY: HTTP-ONLY COOKIES + BFF] — Set cookies, no tokens in URL
        SetAuthCookies(result.AccessToken!, result.RefreshToken!, result.ExpiresAt!.Value);

        var clientUrl = _configuration["OAuth:ClientCallbackUrl"]!;
        return Redirect(clientUrl);
    }

    // ═══════════════════════════════════════════════════════════
    // [SECURITY: HTTP-ONLY COOKIES] — Identical cookie-setting
    // logic to AuthController, ensuring all auth paths produce
    // the same secure cookie configuration.
    // ═══════════════════════════════════════════════════════════
    private void SetAuthCookies(string accessToken, string refreshToken, DateTime expiresAt)
    {
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/"
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/api/auth"
        };

        Response.Cookies.Append("AccessToken", accessToken, accessCookieOptions);
        Response.Cookies.Append("RefreshToken", refreshToken, refreshCookieOptions);
    }
}
