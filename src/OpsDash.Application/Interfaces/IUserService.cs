using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Users;

namespace OpsDash.Application.Interfaces;

public interface IUserService
{
    Task<ApiResponse<PagedResult<UserDto>>> GetUsersAsync(UserSearchRequest search, PagedRequest paging);

    Task<ApiResponse<UserDto>> GetUserByIdAsync(int id);

    Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequest request);

    Task<ApiResponse<UserDto>> UpdateUserAsync(int id, UpdateUserRequest request);

    Task<ApiResponse<bool>> DeleteUserAsync(int id);

    Task<ApiResponse<List<UserDto>>> SearchUsersAsync(string query);

    Task<ApiResponse<List<RoleDto>>> GetRolesAsync();
}
