using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpsDash.Application.DTOs.Auth;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class AuthService : IAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;

    public AuthService(
        IAppDbContext db,
        ITokenService tokenService,
        IConfiguration configuration,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator)
    {
        _db = db;
        _tokenService = tokenService;
        _configuration = configuration;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ApiResponse<AuthResponse>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var subdomain = request.Subdomain.Trim().ToLowerInvariant();
        var email = request.Email.Trim();

        var subdomainTaken = await _db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Subdomain == subdomain);
        if (subdomainTaken)
        {
            return ApiResponse<AuthResponse>.Fail("Subdomain is already in use.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Subdomain = subdomain,
            Plan = "Starter",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var emailTakenInTenant = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenant.Id && u.Email == email);
        if (emailTakenInTenant)
        {
            await transaction.RollbackAsync();
            return ApiResponse<AuthResponse>.Fail("Email is already registered for this tenant.");
        }

        var adminPermissions = JsonSerializer.Serialize(
            new[] { "users.manage", "metrics.manage", "alerts.manage", "incidents.manage", "reports.manage" },
            JsonOptions);
        var userPermissions = JsonSerializer.Serialize(
            new[] { "metrics.view", "alerts.view", "incidents.view" },
            JsonOptions);

        var adminRole = new Role
        {
            TenantId = tenant.Id,
            Name = "Admin",
            Permissions = adminPermissions,
        };
        var defaultUserRole = new Role
        {
            TenantId = tenant.Id,
            Name = "User",
            Permissions = userPermissions,
        };

        _db.Roles.AddRange(adminRole, defaultUserRole);
        await _db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            RoleId = adminRole.Id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var refreshToken = CreateRefreshTokenEntity(user.Id);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        var accessToken = _tokenService.GenerateAccessToken(user, adminRole.Name, tenant.Id);
        var response = BuildAuthResponse(
            accessToken,
            refreshToken.Token,
            user,
            adminRole.Name,
            tenant);

        return ApiResponse<AuthResponse>.Ok(response);
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ApiResponse<AuthResponse>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var subdomain = request.Subdomain.Trim().ToLowerInvariant();
        var email = request.Email.Trim();

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain);
        if (tenant is null)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid credentials.");
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponse>.Fail("Invalid credentials.");
        }

        var activeTokens = await _db.RefreshTokens.IgnoreQueryFilters()
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
        }

        var refreshToken = CreateRefreshTokenEntity(user.Id);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user, user.Role.Name, tenant.Id);
        var response = BuildAuthResponse(
            accessToken,
            refreshToken.Token,
            user,
            user.Role.Name,
            tenant);

        return ApiResponse<AuthResponse>.Ok(response);
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var validation = await _refreshValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ApiResponse<AuthResponse>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
        if (principal is null)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid token.");
        }

        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var tenantIdClaim = principal.FindFirst("tenantId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || !int.TryParse(tenantIdClaim, out var tenantId))
        {
            return ApiResponse<AuthResponse>.Fail("Invalid token.");
        }

        var storedRefresh = await _db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt =>
                rt.UserId == userId
                && rt.Token == request.RefreshToken
                && rt.RevokedAt == null
                && rt.ExpiresAt > DateTime.UtcNow);

        if (storedRefresh is null)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid refresh token.");
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);
        if (user is null)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid token.");
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid token.");
        }

        storedRefresh.RevokedAt = DateTime.UtcNow;

        var newRefresh = CreateRefreshTokenEntity(user.Id);
        _db.RefreshTokens.Add(newRefresh);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user, user.Role.Name, tenant.Id);
        var response = BuildAuthResponse(
            accessToken,
            newRefresh.Token,
            user,
            user.Role.Name,
            tenant);

        return ApiResponse<AuthResponse>.Ok(response);
    }

    public async Task<ApiResponse<bool>> RevokeTokenAsync(TokenRevokeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ApiResponse<bool>.Fail("Refresh token is required.");
        }

        var token = await _db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken.Trim());
        if (token is null)
        {
            return ApiResponse<bool>.Fail("Refresh token was not found.");
        }

        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private RefreshToken CreateRefreshTokenEntity(int userId)
    {
        var refreshDays = int.TryParse(_configuration["JwtSettings:RefreshTokenExpirationDays"], out var d)
            ? d
            : 7;

        return new RefreshToken
        {
            UserId = userId,
            Token = _tokenService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAt = DateTime.UtcNow,
        };
    }

    private AuthResponse BuildAuthResponse(
        string accessToken,
        string refreshTokenValue,
        User user,
        string roleName,
        Tenant tenant)
    {
        var expirationMinutes = int.TryParse(_configuration["JwtSettings:ExpirationMinutes"], out var m)
            ? m
            : 60;

        return new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshTokenValue,
            TokenExpiration = DateTime.UtcNow.AddMinutes(expirationMinutes),
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = roleName,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
        };
    }
}
