using Easebnb.Identity.Core.Dtos;

namespace Easebnb.Identity.Core.Interfaces;

public interface IAuthService
{
    Task<ErrorOr<Success>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress);
    Task<ErrorOr<LoginResponse>> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task<ErrorOr<Success>> RevokeTokenAsync(string refreshToken, string ipAddress);
    Task<ErrorOr<UserInfo>> GetUserInfoAsync(Guid userId);
}