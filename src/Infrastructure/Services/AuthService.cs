using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecurePlatform.Application.Common;
using SecurePlatform.Application.DTOs.Auth;
using SecurePlatform.Application.Interfaces;
using SecurePlatform.Domain.Entities;
using SecurePlatform.Domain.Enums;
using SecurePlatform.Infrastructure.Data;

namespace SecurePlatform.Infrastructure.Services;

// ═══════════════════════════════════════════════════════════════
// [SECURITY: SQL INJECTION] — This service uses Entity Framework
// Core exclusively for database access. EF Core always generates
// parameterized queries, so user input (email, passwords, tokens)
// is never concatenated into raw SQL — structurally preventing
// SQL injection attacks.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Full authentication service implementation.
/// Handles Register, Login, Refresh Token, Logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ITokenRevocationService tokenRevocationService,
        IEmailService emailService,
        ApplicationDbContext dbContext,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _tokenRevocationService = tokenRevocationService;
        _emailService = emailService;
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // 1. Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return AuthResponse.Failure(AuthResultType.DuplicateEmail, "Email is already registered.");

        if (request.Password != request.ConfirmPassword)
            return AuthResponse.Failure(AuthResultType.RegistrationFailed, "Passwords do not match.");

        // 2. Create the user
        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return AuthResponse.Failure(AuthResultType.RegistrationFailed, errors);
        }

        // 3. Assign default role
        await _userManager.AddToRoleAsync(user, "User");

        // 4. Send email confirmation
        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailService.SendEmailConfirmationAsync(user.Email!, confirmToken);

        return AuthResponse.Failure(AuthResultType.EmailNotConfirmed,
            "Registration successful. Please check your email to confirm your account.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. Find user
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return AuthResponse.Failure(AuthResultType.InvalidCredentials, "Invalid email or password.");

        if (!user.IsActive)
            return AuthResponse.Failure(AuthResultType.UserLocked, "Account is deactivated.");

        // Check email confirmation
        if (!user.EmailConfirmed)
            return AuthResponse.Failure(AuthResultType.EmailNotConfirmed, "Please confirm your email before logging in.");

        // 2. Verify password
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
            return AuthResponse.Failure(AuthResultType.InvalidCredentials, "Invalid email or password.");

        // 3. Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // 4. Generate tokens
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // 1. Get principal from expired access token
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
            return AuthResponse.Failure(AuthResultType.TokenInvalid, "Invalid access token.");

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return AuthResponse.Failure(AuthResultType.TokenInvalid, "Invalid token claims.");

        // 2. Find and validate the refresh token in DB
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (storedToken == null || !storedToken.IsActive)
            return AuthResponse.Failure(AuthResultType.TokenInvalid, "Invalid or expired refresh token.");

        // 3. Rotate: revoke old, issue new
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReasonRevoked = "Replaced by new token";

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return AuthResponse.Failure(AuthResultType.UserNotFound, "User not found.");

        var newRefreshToken = CreateRefreshToken(user.Id);
        storedToken.ReplacedByToken = newRefreshToken.Token;

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync();

        // 4. Generate new access token
        var claims = await GetClaimsAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(claims);

        var roles = await _userManager.GetRolesAsync(user);
        return AuthResponse.Success(
            newAccessToken,
            newRefreshToken.Token,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            MapToUserDto(user, roles));
    }

    public async Task<bool> LogoutAsync(string userId, string? accessTokenJti = null)
    {
        // 1. Revoke the current access token in Redis so it's rejected immediately
        if (!string.IsNullOrEmpty(accessTokenJti))
        {
            var remainingLifetime = TimeSpan.FromMinutes(_jwtSettings.AccessTokenExpirationMinutes);
            await _tokenRevocationService.RevokeTokenAsync(accessTokenJti, remainingLifetime);
        }

        // 2. Revoke ALL active refresh tokens for the user (secure logout)
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.ReasonRevoked = "User logged out";
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> GetCurrentUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return MapToUserDto(user, roles);
    }

    // ═══════════════════════════════════════════════
    // Password Reset
    // ═══════════════════════════════════════════════

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal whether the email exists — always return success message
            return AuthResponse.Failure(AuthResultType.PasswordResetCodeSent,
                "If that email is registered, a reset code has been sent.");
        }

        // Generate a 6-digit code using Identity's token provider
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Send the code via email (DevEmailService logs to console in dev)
        await _emailService.SendPasswordResetCodeAsync(user.Email!, token);

        return AuthResponse.Failure(AuthResultType.PasswordResetCodeSent,
            "If that email is registered, a reset code has been sent.");
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return AuthResponse.Failure(AuthResultType.PasswordResetFailed, "Passwords do not match.");

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return AuthResponse.Failure(AuthResultType.PasswordResetFailed, "Invalid reset request.");

        var result = await _userManager.ResetPasswordAsync(user, request.Code, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return AuthResponse.Failure(AuthResultType.PasswordResetFailed, errors);
        }

        return AuthResponse.Failure(AuthResultType.PasswordResetSuccess,
            "Password has been reset successfully. You can now login.");
    }

    // ═══════════════════════════════════════════════
    // Email Confirmation
    // ═══════════════════════════════════════════════

    public async Task<AuthResponse> ConfirmEmailAsync(string email, string token)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return AuthResponse.Failure(AuthResultType.UserNotFound, "Invalid confirmation request.");

        if (user.EmailConfirmed)
            return AuthResponse.Success(string.Empty, string.Empty, DateTime.UtcNow,
                MapToUserDto(user, await _userManager.GetRolesAsync(user)));

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return AuthResponse.Failure(AuthResultType.TokenInvalid, errors);
        }

        // Auto-login after confirmation
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> ResendConfirmationEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal whether the email exists
            return AuthResponse.Failure(AuthResultType.EmailNotConfirmed,
                "If that email is registered, a confirmation email has been sent.");
        }

        if (user.EmailConfirmed)
            return AuthResponse.Failure(AuthResultType.EmailNotConfirmed, "Email is already confirmed.");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailService.SendEmailConfirmationAsync(user.Email!, token);

        return AuthResponse.Failure(AuthResultType.EmailNotConfirmed,
            "If that email is registered, a confirmation email has been sent.");
    }

    // ═══════════════════════════════════════════════
    // OAuth External Login
    // ═══════════════════════════════════════════════

    public async Task<AuthResponse> ExternalLoginAsync(string provider, string email, string firstName, string lastName)
    {
        // 1. Check if user already exists
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            // 2. Auto-register new users from OAuth
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true // OAuth emails are pre-verified
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return AuthResponse.Failure(AuthResultType.RegistrationFailed, errors);
            }

            await _userManager.AddToRoleAsync(user, "User");
        }

        if (!user.IsActive)
            return AuthResponse.Failure(AuthResultType.UserLocked, "Account is deactivated.");

        // 3. Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // 4. Generate JWT tokens (same as normal login)
        return await GenerateAuthResponseAsync(user);
    }

    // ═══════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var claims = await GetClaimsAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(claims);
        var refreshToken = CreateRefreshToken(user.Id);

        // Save refresh token in DB
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);

        return AuthResponse.Success(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            MapToUserDto(user, roles));
    }

    private async Task<List<Claim>> GetClaimsAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    private RefreshToken CreateRefreshToken(string userId)
    {
        return new RefreshToken
        {
            Token = _tokenService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            UserId = userId
        };
    }

    private static UserDto MapToUserDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles
        };
    }
}
