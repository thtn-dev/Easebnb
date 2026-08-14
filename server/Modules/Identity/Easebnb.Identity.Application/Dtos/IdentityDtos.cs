namespace Easebnb.Identity.Core.Dtos;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string ConfirmPassword);

public record LoginRequest(
    string Username,
    string Password,
    bool RememberMe = false);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserInfo User);

public record RefreshTokenRequest(
    string RefreshToken);

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool TwoFactorEnabled);

// Change Password
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

// Forgot Password
public record ForgotPasswordRequest(
    string Email);

// Reset Password
public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword);

// Email Confirmation
public record ConfirmEmailRequest(
    string UserId,
    string Token);

public record ResendEmailConfirmationRequest(
    string Email);

// Profile Management
public record UpdateProfileRequest(
    string? Email,
    string? PhoneNumber);

// Two-Factor Authentication
public record Enable2FAResponse(
    string SharedKey,
    string AuthenticatorUri);

public record Verify2FARequest(
    string Code);

public record GenerateRecoveryCodesResponse(
    string[] RecoveryCodes);