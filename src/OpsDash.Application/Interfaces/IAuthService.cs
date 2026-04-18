using OpsDash.Application.DTOs.Auth;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);

    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);

    Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request);

    Task<ApiResponse<bool>> RevokeTokenAsync(TokenRevokeRequest request);
}
