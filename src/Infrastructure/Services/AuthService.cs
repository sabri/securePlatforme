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

/// <summary>
/// Full authentication service implementation.
/// Handles Register, Login, Refresh Token, Logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ApplicationDbContext dbContext,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _tokenService = tokenService;
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

        // 4. Generate tokens and return
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. Find user
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return AuthResponse.Failure(AuthResultType.InvalidCredentials, "Invalid email or password.");

        if (!user.IsActive)
            return AuthResponse.Failure(AuthResultType.UserLocked, "Account is deactivated.");

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

    public async Task<bool> LogoutAsync(string userId)
    {
        // Revoke ALL active refresh tokens for the user (secure logout)
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
