using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Database;

namespace Easebnb.Identity.Infrastructure.Services;

public class SendEmailEvent : DomainEventBase
{
    public SendEmailEvent(string email, string subject, string body)
    {
        Email = email;
        Subject = subject;
        Body = body;
    }

    public string Email { get; }
    public string Subject { get; }
    public string Body { get; }
}

public class SendMailHandler(ILogger<SendMailHandler> logger) : INotificationHandler<SendEmailEvent>
{
    public async Task Handle(SendEmailEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Sending email to {Email} with subject '{Subject}' and body '{Body}'", notification.Email,
            notification.Subject, notification.Body);
        // Simulate email sending delay
        await Task.Delay(10000, cancellationToken);
    }
}

public class AuthService(
    UserManager<User> userManager,
    AppIdentityDbContext dbContext,
    IUnitOfWork unitOfWork,
    IJwtService jwtService)
    : IAuthService
{
    public async Task<ErrorOr<Success>> RegisterAsync(RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
            return Error.Validation("Passwords do not match");

        var existingUser = await userManager.FindByNameAsync(request.Username);
        if (existingUser != null)
            return Error.Conflict("Username already exists");

        var existingEmail = await userManager.FindByEmailAsync(request.Email);
        if (existingEmail != null)
            return Error.Conflict("Email already exists");

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            UserName = request.Username,
            Email = request.Email,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(x => x.Code, x => x.Description);
            return Error.Validation(errors.First().Key);
        }

        await userManager.AddToRoleAsync(user, "user");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var emailEvent = new SendEmailEvent(user.Email!, "Confirm your email",
            "Please confirm your email by clicking the link.");
        user.AddDomainEvent(emailEvent);
        await unitOfWork.CommitTransactionAsync(cancellationToken);
        return Result.Success;
    }

    public async Task<ErrorOr<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress)
    {
        // Find user
        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null)
            return Error.Unauthorized("Invalid username or password");

        if (await userManager.IsLockedOutAsync(user))
            return Error.Forbidden("Account is locked out. Please try again later.");

        if (!user.EmailConfirmed && userManager.Options.SignIn.RequireConfirmedEmail)
            return Error.Unauthorized("Email not confirmed. Please confirm your email first.");

        var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);

        if (!isPasswordValid)
        {
            await userManager.AccessFailedAsync(user);

            if (await userManager.IsLockedOutAsync(user))
                return Error.Forbidden("Account is locked out due to multiple failed login attempts.");

            return Error.Unauthorized("Invalid username or password");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);

        // Generate JWT token
        var accessToken = jwtService.GenerateToken(
            user.Id.ToString(),
            user.UserName!,
            [.. roles]);

        // Generate refresh token
        var refreshTokenValue = jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };
        _ = await AddRefreshTokenAsync(refreshToken, user.Id);

        var userInfo = new UserInfo(
            user.Id,
            user.UserName!,
            user.Email!,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.TwoFactorEnabled);

        var loginResponse = new LoginResponse(
            accessToken,
            refreshTokenValue,
            "Bearer",
            3600,
            userInfo);

        return loginResponse;
    }

    public async Task<ErrorOr<LoginResponse>> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var token = await GetRefreshTokenAsync(refreshToken);

        if (token is null || !token.IsActive())
            return Error.Unauthorized("Invalid or expired refresh token");

        var user = token.User;

        var roles = await userManager.GetRolesAsync(user);
        var newAccessToken = jwtService.GenerateToken(
            user.Id.ToString(),
            user.UserName!,
            [.. roles]);

        var newRefreshToken = jwtService.GenerateRefreshToken();

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        token.ReplacedByToken = newRefreshToken;

        // Create new refresh token
        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        _ = await AddRefreshTokenAsync(refreshTokenEntity, user.Id);
        var userInfo = new UserInfo(
            user.Id,
            user.UserName!,
            user.Email!,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.TwoFactorEnabled);

        var loginResponse = new LoginResponse(
            newAccessToken,
            newRefreshToken,
            "Bearer",
            3600,
            userInfo);

        return loginResponse;
    }

    public async Task<ErrorOr<Success>> RevokeTokenAsync(string refreshToken, string ipAddress)
    {
        var token = await GetRefreshTokenAsync(refreshToken);

        if (token is null || !token.IsActive())
            return Error.NotFound("Invalid refresh token");

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;

        return Result.Success;
    }

    public async Task<ErrorOr<UserInfo>> GetUserInfoAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Error.NotFound("User not found");

        var userInfo = new UserInfo(
            user.Id,
            user.UserName!,
            user.Email!,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.TwoFactorEnabled);

        return userInfo;
    }

    private async Task<bool> AddRefreshTokenAsync(RefreshTokenEntity refreshToken, Guid userId)
    {
        var oldTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(5)
            .ToListAsync();

        dbContext.RefreshTokens.RemoveRange(oldTokens);
        await dbContext.RefreshTokens.AddAsync(refreshToken);
        var rs = await dbContext.SaveChangesAsync();
        return rs > 0;
    }

    private async Task<RefreshTokenEntity?> GetRefreshTokenAsync(string refreshToken)
    {
        var token = await dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);
        return token;
    }
}