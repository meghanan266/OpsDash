using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Users;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class UserService : IUserService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ITenantContextService _tenantContext;

    public UserService(IAppDbContext db, IMapper mapper, ITenantContextService tenantContext)
    {
        _db = db;
        _mapper = mapper;
        _tenantContext = tenantContext;
    }

    public async Task<ApiResponse<PagedResult<UserDto>>> GetUsersAsync(UserSearchRequest search, PagedRequest paging)
    {
        search ??= new UserSearchRequest();
        paging ??= new PagedRequest();

        IQueryable<User> query = _db.Users;

        if (!string.IsNullOrWhiteSpace(search.SearchTerm))
        {
            var term = search.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term)
                || u.FirstName.ToLower().Contains(term)
                || u.LastName.ToLower().Contains(term));
        }

        if (search.RoleId is int filterRoleId)
        {
            query = query.Where(u => u.RoleId == filterRoleId);
        }

        if (search.IsActive is bool filterActive)
        {
            query = query.Where(u => u.IsActive == filterActive);
        }

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, paging);

        var items = await query
            .Include(u => u.Role)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        var paged = new PagedResult<UserDto>
        {
            Items = _mapper.Map<List<UserDto>>(items),
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize,
        };

        return ApiResponse<PagedResult<UserDto>>.Ok(paged);
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(int id)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            return ApiResponse<UserDto>.Fail("User not found");
        }

        return ApiResponse<UserDto>.Ok(_mapper.Map<UserDto>(user));
    }

    public async Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        var email = request.Email.Trim();
        var emailTaken = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLowerInvariant());
        if (emailTaken)
        {
            return ApiResponse<UserDto>.Fail("A user with this email already exists");
        }

        var roleExists = await _db.Roles.AnyAsync(r => r.Id == request.RoleId);
        if (!roleExists)
        {
            return ApiResponse<UserDto>.Fail("Role not found");
        }

        var user = _mapper.Map<User>(request);
        user.Email = email;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.TenantId = _tenantContext.TenantId;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var created = await _db.Users
            .Include(u => u.Role)
            .FirstAsync(u => u.Id == user.Id);

        return ApiResponse<UserDto>.Ok(_mapper.Map<UserDto>(created));
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return ApiResponse<UserDto>.Fail("User not found");
        }

        if (request.FirstName is not null)
        {
            user.FirstName = request.FirstName.Trim();
        }

        if (request.LastName is not null)
        {
            user.LastName = request.LastName.Trim();
        }

        if (request.RoleId is int newRoleId)
        {
            var roleExists = await _db.Roles.AnyAsync(r => r.Id == newRoleId);
            if (!roleExists)
            {
                return ApiResponse<UserDto>.Fail("Role not found");
            }

            user.RoleId = newRoleId;
        }

        if (request.IsActive is bool active)
        {
            user.IsActive = active;
        }

        await _db.SaveChangesAsync();

        var updated = await _db.Users
            .Include(u => u.Role)
            .FirstAsync(u => u.Id == id);

        return ApiResponse<UserDto>.Ok(_mapper.Map<UserDto>(updated));
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return ApiResponse<bool>.Fail("User not found");
        }

        user.IsActive = false;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<UserDto>>> SearchUsersAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ApiResponse<List<UserDto>>.Ok([]);
        }

        var term = query.Trim().ToLowerInvariant();
        var users = await _db.Users
            .Include(u => u.Role)
            .Where(u =>
                u.Email.ToLower().Contains(term)
                || u.FirstName.ToLower().Contains(term)
                || u.LastName.ToLower().Contains(term))
            .Take(20)
            .ToListAsync();

        return ApiResponse<List<UserDto>>.Ok(_mapper.Map<List<UserDto>>(users));
    }

    private static IQueryable<User> ApplySorting(IQueryable<User> query, PagedRequest paging)
    {
        var sortKey = string.IsNullOrWhiteSpace(paging.SortBy)
            ? "createdat"
            : paging.SortBy.Trim().ToLowerInvariant();

        var desc = string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortKey switch
        {
            "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "firstname" => desc
                ? query.OrderByDescending(u => u.FirstName)
                : query.OrderBy(u => u.FirstName),
            "lastname" => desc
                ? query.OrderByDescending(u => u.LastName)
                : query.OrderBy(u => u.LastName),
            "createdat" => desc
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),
            _ => desc
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),
        };
    }
}
