using System.Security.Claims;
using BuildingBlocks.SharedKernel;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Identity.UnitTests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly AppIdentityDbContext _dbContext;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<User>>();
        // UserManager has many ctor params; only the store is required to be non-null.
        _userManagerMock = new Mock<UserManager<User>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppIdentityDbContext(options);
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _jwtServiceMock = new Mock<IJwtService>();

        _sut = new AuthService(_userManagerMock.Object, _dbContext, _unitOfWorkMock.Object, _jwtServiceMock.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private static User CreateUser(
        Guid? id = null,
        string username = "user",
        string email = "user@test.com",
        bool emailConfirmed = true) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            UserName = username,
            Email = email,
            EmailConfirmed = emailConfirmed,
            PhoneNumber = "0123456789",
            TwoFactorEnabled = false
        };

    private static RefreshTokenEntity CreateRefreshToken(
        Guid userId,
        string token,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        bool revoked = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-1),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(6),
            IsRevoked = revoked,
            CreatedByIp = "seed"
        };

    private void SetupJwtGeneration(string accessToken = "access-token", string refreshToken = "refresh-token")
    {
        _jwtServiceMock
            .Setup(j => j.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<Claim>?>()))
            .Returns(accessToken);
        _jwtServiceMock
            .Setup(j => j.GenerateRefreshToken())
            .Returns(refreshToken);
    }


    // ---------------------------------------------------------------
    // RegisterAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WhenPasswordsDoNotMatch_ReturnsValidationErrorWithoutTouchingStore()
    {
        var request = new RegisterRequest("user", "user@test.com", "Pass123!", "Different123!");

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Passwords do not match");
        _userManagerMock.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameAlreadyTaken_ReturnsConflict()
    {
        var request = new RegisterRequest("user", "user@test.com", "Pass123!", "Pass123!");
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(CreateUser(username: "user"));

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Username already exists");
        _userManagerMock.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never,
            "a taken username must short-circuit before the email lookup");
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyRegistered_ReturnsConflict()
    {
        var request = new RegisterRequest("user", "user@test.com", "Pass123!", "Pass123!");
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@test.com"))
            .ReturnsAsync(CreateUser(email: "user@test.com"));

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Email already exists");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenUserCreationFails_ReturnsValidationErrorAndRollsBack()
    {
        var request = new RegisterRequest("user", "user@test.com", "weak", "weak");
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@test.com"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<User>(), "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PasswordTooShort");
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once,
            "a failed creation must release the open transaction");
    }

    [Fact]
    public async Task RegisterAsync_WhenAddToRoleFails_ReturnsErrorAndRollsBack()
    {
        var request = new RegisterRequest("user", "user@test.com", "Pass123!", "Pass123!");
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@test.com"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<User>(), "Pass123!"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<User>(), "user"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role does not exist" }));

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeTrue();
        _userManagerMock.Verify(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()), Times.Never,
            "no confirmation email should be queued when the role assignment fails");
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenSucceeds_AddsRoleAndQueuesConfirmationEmailEvent()
    {
        var request = new RegisterRequest("newuser", "new@test.com", "Pass123!", "Pass123!");
        User? createdUser = null;
        _userManagerMock
            .Setup(m => m.FindByNameAsync("newuser"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("new@test.com"))
            .ReturnsAsync((User?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<User>(), "Pass123!"))
            .Callback<User, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<User>(), "user"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
            .ReturnsAsync("confirmation-token");

        var result = await _sut.RegisterAsync(request);

        result.IsError.Should().BeFalse();
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), "user"), Times.Once);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        createdUser.Should().NotBeNull();
        createdUser!.EmailConfirmed.Should().BeFalse();
        var emailEvent = createdUser.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SendEmailEvent>().Subject;
        emailEvent.Email.Should().Be("new@test.com");
        emailEvent.Subject.Should().Be("Confirm your email");
        emailEvent.Body.Should().Be("Please confirm your email by clicking the link.");
    }

    // ---------------------------------------------------------------
    // LoginAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock
            .Setup(m => m.FindByNameAsync("ghost"))
            .ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(new LoginRequest("ghost", "Pass123!"), "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_WhenAccountLockedOut_ReturnsForbidden()
    {
        var user = CreateUser();
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(true);

        var result = await _sut.LoginAsync(new LoginRequest("user", "Pass123!"), "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account is locked out. Please try again later.");
        _userManagerMock.Verify(m => m.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never,
            "a locked account must not have its password checked");
    }

    [Fact]
    public async Task LoginAsync_WhenEmailNotConfirmedAndConfirmationRequired_ReturnsUnauthorized()
    {
        var user = CreateUser(emailConfirmed: false);
        _userManagerMock.Object.Options.SignIn.RequireConfirmedEmail = true;
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(false);

        var result = await _sut.LoginAsync(new LoginRequest("user", "Pass123!"), "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Email not confirmed. Please confirm your email first.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordWrong_ReturnsUnauthorizedAndCountsFailure()
    {
        var user = CreateUser();
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "WrongPass!"))
            .ReturnsAsync(false);

        var result = await _sut.LoginAsync(new LoginRequest("user", "WrongPass!"), "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid username or password");
        _userManagerMock.Verify(m => m.AccessFailedAsync(user), Times.Once,
            "each failed attempt must count toward lockout");
        _userManagerMock.Verify(m => m.ResetAccessFailedCountAsync(user), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenFailedAttemptLocksAccount_ReturnsForbidden()
    {
        var user = CreateUser();
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .SetupSequence(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "WrongPass!"))
            .ReturnsAsync(false);

        var result = await _sut.LoginAsync(new LoginRequest("user", "WrongPass!"), "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account is locked out due to multiple failed login attempts.");
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsTokensAndPersistsRefreshToken()
    {
        var user = CreateUser();
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "Pass123!"))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(["admin"]);
        SetupJwtGeneration();
        var before = DateTime.UtcNow.AddMinutes(-1);

        var result = await _sut.LoginAsync(new LoginRequest("user", "Pass123!"), "203.0.113.5");

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().Be("refresh-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(3600);
        response.User.Id.Should().Be(user.Id);
        response.User.Username.Should().Be("user");
        response.User.Email.Should().Be("user@test.com");
        response.User.EmailConfirmed.Should().BeTrue();
        response.User.PhoneNumber.Should().Be("0123456789");
        response.User.TwoFactorEnabled.Should().BeFalse();
        _userManagerMock.Verify(m => m.ResetAccessFailedCountAsync(user), Times.Once,
            "a successful login must clear the failure counter");
        var stored = _dbContext.RefreshTokens.AsNoTracking().Single();
        stored.UserId.Should().Be(user.Id);
        stored.Token.Should().Be("refresh-token");
        stored.CreatedByIp.Should().Be("203.0.113.5");
        stored.ExpiresAt.Should().BeOnOrAfter(DateTime.UtcNow.AddDays(7).AddMinutes(-1));
        stored.ExpiresAt.Should().BeOnOrBefore(DateTime.UtcNow.AddDays(7).AddMinutes(1));
        stored.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasMoreThanFiveRefreshTokens_PrunesOldestBeyondLatestFive()
    {
        var user = CreateUser();
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.IsLockedOutAsync(user))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "Pass123!"))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());
        SetupJwtGeneration();
        _dbContext.Users.Add(user);
        for (var i = 30; i >= 25; i--)
            _dbContext.RefreshTokens.Add(CreateRefreshToken(user.Id, $"old-{i}",
                createdAt: DateTime.UtcNow.AddDays(-i)));
        _dbContext.SaveChanges();

        var result = await _sut.LoginAsync(new LoginRequest("user", "Pass123!"), "127.0.0.1");

        result.IsError.Should().BeFalse();
        var tokens = _dbContext.RefreshTokens.AsNoTracking().ToList();
        tokens.Should().HaveCount(6, "five latest tokens plus the new one must remain");
        tokens.Select(t => t.Token).Should().NotContain("old-30",
            "the oldest token beyond the latest five must be pruned");
        tokens.Select(t => t.Token).Should().Contain(["old-29", "old-28", "old-27", "old-26", "old-25", "refresh-token"]);
    }

    // ---------------------------------------------------------------
    // GetUserInfoAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetUserInfoAsync_WhenUserExists_ReturnsUserInfo()
    {
        var userId = Guid.NewGuid();
        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(CreateUser(userId));

        var result = await _sut.GetUserInfoAsync(userId);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(userId);
        result.Value.Username.Should().Be("user");
        result.Value.Email.Should().Be("user@test.com");
        result.Value.PhoneNumber.Should().Be("0123456789");
        result.Value.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenUserMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((User?)null);

        var result = await _sut.GetUserInfoAsync(userId);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User not found");
    }

    // ---------------------------------------------------------------
    // RefreshTokenAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenUnknown_ReturnsUnauthorized()
    {
        var result = await _sut.RefreshTokenAsync("unknown-token", "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid or expired refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenRevoked_ReturnsUnauthorized()
    {
        var user = CreateUser();
        SeedUserWithToken(user, CreateRefreshToken(user.Id, "revoked-token", revoked: true));

        var result = await _sut.RefreshTokenAsync("revoked-token", "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid or expired refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenExpired_ReturnsUnauthorized()
    {
        var user = CreateUser();
        SeedUserWithToken(user, CreateRefreshToken(user.Id, "expired-token", expiresAt: DateTime.UtcNow.AddHours(-1)));

        var result = await _sut.RefreshTokenAsync("expired-token", "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid or expired refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenActive_RotatesTokenAndRevokesOldOne()
    {
        var user = CreateUser();
        var token = CreateRefreshToken(user.Id, "current-token");
        SeedUserWithToken(user, token);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(["user"]);
        SetupJwtGeneration("new-access-token", "new-refresh-token");

        var result = await _sut.RefreshTokenAsync("current-token", "198.51.100.7");

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.AccessToken.Should().Be("new-access-token");
        response.RefreshToken.Should().Be("new-refresh-token");
        response.User.Id.Should().Be(user.Id);
        var stored = _dbContext.RefreshTokens.AsNoTracking().ToList();
        stored.Should().HaveCount(2);
        var oldToken = stored.Single(t => t.Token == "current-token");
        oldToken.IsRevoked.Should().BeTrue("using a refresh token must revoke it");
        oldToken.RevokedByIp.Should().Be("198.51.100.7");
        oldToken.ReplacedByToken.Should().Be("new-refresh-token");
        oldToken.RevokedAt.Should().NotBeNull();
        var newToken = stored.Single(t => t.Token == "new-refresh-token");
        newToken.CreatedByIp.Should().Be("198.51.100.7");
        newToken.UserId.Should().Be(user.Id);
    }

    // ---------------------------------------------------------------
    // RevokeTokenAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RevokeTokenAsync_WhenTokenUnknown_ReturnsNotFound()
    {
        var result = await _sut.RevokeTokenAsync("unknown-token", "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RevokeTokenAsync_WhenTokenAlreadyRevoked_ReturnsNotFound()
    {
        var user = CreateUser();
        SeedUserWithToken(user, CreateRefreshToken(user.Id, "revoked-token", revoked: true));

        var result = await _sut.RevokeTokenAsync("revoked-token", "127.0.0.1");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RevokeTokenAsync_WhenTokenActive_RevokesToken()
    {
        var user = CreateUser();
        var token = CreateRefreshToken(user.Id, "active-token");
        SeedUserWithToken(user, token);

        var result = await _sut.RevokeTokenAsync("active-token", "198.51.100.9");

        result.IsError.Should().BeFalse();
        token.IsRevoked.Should().BeTrue();
        token.RevokedByIp.Should().Be("198.51.100.9");
        token.RevokedAt.Should().NotBeNull();
        var reloaded = await _dbContext.RefreshTokens.AsNoTracking().SingleAsync(t => t.Token == "active-token");
        reloaded.IsRevoked.Should().BeTrue("the revocation must be persisted, not just tracked");
    }

    private void SeedUserWithToken(User user, RefreshTokenEntity token)
    {
        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(token);
        _dbContext.SaveChanges();
    }
}
