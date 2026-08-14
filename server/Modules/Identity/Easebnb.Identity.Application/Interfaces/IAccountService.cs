using Easebnb.Identity.Core.Dtos;

namespace Easebnb.Identity.Core.Interfaces;

public interface IAccountService
{
    Task<ErrorOr<Success>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ErrorOr<Success>> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<ErrorOr<Success>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ErrorOr<Success>> ConfirmEmailAsync(ConfirmEmailRequest request);
    Task<ErrorOr<Success>> ResendEmailConfirmationAsync(ResendEmailConfirmationRequest request);
    Task<ErrorOr<UserInfo>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
}