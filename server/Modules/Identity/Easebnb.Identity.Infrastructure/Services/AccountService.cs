using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Core.Interfaces;

namespace Easebnb.Identity.Infrastructure.Services;

public class AccountService(UserManager<User> userManager) : IAccountService
{
    public async Task<ErrorOr<Success>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword) return Error.Unexpected("New passwords do not match");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Error.NotFound("User not found");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (result.Succeeded) return Result.Success;
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return Error.Validation("Password change failed: " + errors);
    }

    public async Task<ErrorOr<Success>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Don't reveal if user exists or not for security reasons
        if (user == null) return Result.Success;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // Encode token for URL
        var _ = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // TODO: Send email with reset link
        // Example link: https://yourdomain.com/reset-password?email={email}&token={encodedToken}

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword) return Error.Validation("Passwords do not match");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null) return Error.Validation("Invalid request");

        // Decode token from URL
        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

        if (result.Succeeded) return Result.Success;
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return Error.Validation($"Password reset failed: {errors}");
    }

    public async Task<ErrorOr<Success>> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null) return Error.Validation("Invalid request");

        // Decode token from URL
        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);

        if (result.Succeeded) return Result.Success;
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return Error.Validation($"Email confirmation failed: {errors}");
    }

    public async Task<ErrorOr<Success>> ResendEmailConfirmationAsync(ResendEmailConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Don't reveal if user exists or not
        if (user == null)
            return Result.Success;

        if (user.EmailConfirmed) return Error.Validation("Email is already confirmed");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

        // Encode token for URL
        var _ = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // TODO: Send email with confirmation link
        // Example link: https://yourdomain.com/confirm-email?userId={userId}&token={encodedToken}

        return Result.Success;
    }

    public async Task<ErrorOr<UserInfo>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Error.Validation("User not found");

        var updated = false;

        // Update email if provided and different
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            // Check if email is already taken
            var existingUser = await userManager.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != userId)
                return Error.Validation("Email is already taken");

            var token = await userManager.GenerateChangeEmailTokenAsync(user, request.Email);
            var result = await userManager.ChangeEmailAsync(user, request.Email, token);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Error.Validation($"Email update failed: {errors}");
            }

            updated = true;
        }

        // Update phone number if provided and different
        if (request.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = request.PhoneNumber;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Error.Validation($"Phone number update failed: {errors}");
            }

            updated = true;
        }

        if (!updated) return Error.Validation("No changes were made");

        var userInfo = new UserInfo(
            user.Id,
            user.UserName!,
            user.Email!,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.TwoFactorEnabled);

        return userInfo;
    }
}