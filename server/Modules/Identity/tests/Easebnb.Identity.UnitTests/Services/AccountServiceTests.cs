using System.Text;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using ErrorOr;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Easebnb.Identity.UnitTests.Services;

public class AccountServiceTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<IObjectStorage> _objectStorageMock;
    private readonly AccountService _sut;
    public AccountServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<User>>();
        // UserManager has many ctor params; only the store is required to be non-null.
        _userManagerMock = new Mock<UserManager<User>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
 
        _objectStorageMock = new Mock<IObjectStorage>();
 
        _sut = new AccountService(_userManagerMock.Object, _objectStorageMock.Object);
    }
    
    private static User CreateUser(Guid? id = null, string email = "user@test.com") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            UserName = "user",
            Email = email,
            EmailConfirmed = false,
            PhoneNumber = null,
            TwoFactorEnabled = false
        };
    
    
    // ---------------------------------------------------------------
    // ChangePasswordAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task ChangePasswordAsync_WhenPasswordsDoNotMatch_ReturnsValidationErrorWithoutTouchingStore()
    {
        var request = new ChangePasswordRequest("old", "new1", "new2");

        var result = await _sut.ChangePasswordAsync(Guid.NewGuid(), request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Description.Should().Be("New passwords do not match");
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }
 
    [Fact]
    public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var request = new ChangePasswordRequest("old", "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.ChangePasswordAsync(userId, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("User not found");
    }
 
    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityFails_ReturnsValidationErrorWithMessages()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new ChangePasswordRequest("wrongOld", "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));
 
        var result = await _sut.ChangePasswordAsync(userId, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("Incorrect password.");
    }
 
    [Fact]
    public async Task ChangePasswordAsync_WhenSucceeds_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new ChangePasswordRequest("old", "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.ChangePasswordAsync(userId, request);
 
        result.IsError.Should().BeFalse();
    }
 
    // ---------------------------------------------------------------
    // ForgotPasswordAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task ForgotPasswordAsync_WhenUserDoesNotExist_StillReturnsSuccess()
    {
        var request = new ForgotPasswordRequest("missing@test.com");
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.ForgotPasswordAsync(request);
 
        result.IsError.Should().BeFalse("the API must not leak whether an email is registered");
        _userManagerMock.Verify(m => m.GeneratePasswordResetTokenAsync(It.IsAny<User>()), Times.Never);
    }
 
    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_GeneratesTokenAndReturnsSuccess()
    {
        var request = new ForgotPasswordRequest("user@test.com");
        var user = CreateUser(email: request.Email);
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");
 
        var result = await _sut.ForgotPasswordAsync(request);
 
        result.IsError.Should().BeFalse();
        _userManagerMock.Verify(m => m.GeneratePasswordResetTokenAsync(user), Times.Once);
    }
 
    // ---------------------------------------------------------------
    // ResetPasswordAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordsDoNotMatch_ReturnsValidationError()
    {
        var request = new ResetPasswordRequest("user@test.com", "token", "new1", "new2");
 
        var result = await _sut.ResetPasswordAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Passwords do not match");
    }
 
    [Fact]
    public async Task ResetPasswordAsync_WhenUserNotFound_ReturnsGenericValidationError()
    {
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("token"));
        var request = new ResetPasswordRequest("missing@test.com", token, "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.ResetPasswordAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Invalid request");
    }
 
    [Fact]
    public async Task ResetPasswordAsync_WhenIdentityFails_ReturnsValidationErrorWithMessages()
    {
        var user = CreateUser(email: "user@test.com");
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("raw-token"));
        var request = new ResetPasswordRequest(user.Email!, encodedToken, "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ResetPasswordAsync(user, "raw-token", request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));
 
        var result = await _sut.ResetPasswordAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("Invalid token.");
    }
 
    [Fact]
    public async Task ResetPasswordAsync_WhenSucceeds_ReturnsSuccess()
    {
        var user = CreateUser(email: "user@test.com");
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("raw-token"));
        var request = new ResetPasswordRequest(user.Email!, encodedToken, "new1", "new1");
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ResetPasswordAsync(user, "raw-token", request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.ResetPasswordAsync(request);
 
        result.IsError.Should().BeFalse();
    }
 
    // ---------------------------------------------------------------
    // ConfirmEmailAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task ConfirmEmailAsync_WhenUserNotFound_ReturnsValidationError()
    {
        var request = new ConfirmEmailRequest(Guid.NewGuid(), "token");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(request.UserId.ToString()))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.ConfirmEmailAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Invalid request");
    }
 
    [Fact]
    public async Task ConfirmEmailAsync_WhenIdentityFails_ReturnsValidationErrorWithMessages()
    {
        var user = CreateUser();
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("raw-token"));
        var request = new ConfirmEmailRequest(user.Id, encodedToken);
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ConfirmEmailAsync(user, "raw-token"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));
 
        var result = await _sut.ConfirmEmailAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("Invalid token.");
    }
 
    [Fact]
    public async Task ConfirmEmailAsync_WhenSucceeds_ReturnsSuccess()
    {
        var user = CreateUser();
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("raw-token"));
        var request = new ConfirmEmailRequest(user.Id, encodedToken);
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ConfirmEmailAsync(user, "raw-token"))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.ConfirmEmailAsync(request);
 
        result.IsError.Should().BeFalse();
    }
 
    // ---------------------------------------------------------------
    // ResendEmailConfirmationAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenUserNotFound_ReturnsSuccess()
    {
        var request = new ResendEmailConfirmationRequest("missing@test.com");
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.ResendEmailConfirmationAsync(request);
 
        result.IsError.Should().BeFalse();
    }
 
    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenAlreadyConfirmed_ReturnsValidationError()
    {
        var user = CreateUser();
        user.EmailConfirmed = true;
        var request = new ResendEmailConfirmationRequest(user.Email!);
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
 
        var result = await _sut.ResendEmailConfirmationAsync(request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Email is already confirmed");
    }
 
    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenNotConfirmed_GeneratesTokenAndReturnsSuccess()
    {
        var user = CreateUser();
        user.EmailConfirmed = false;
        var request = new ResendEmailConfirmationRequest(user.Email!);
 
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("confirm-token");
 
        var result = await _sut.ResendEmailConfirmationAsync(request);
 
        result.IsError.Should().BeFalse();
        _userManagerMock.Verify(m => m.GenerateEmailConfirmationTokenAsync(user), Times.Once);
    }
 
    // ---------------------------------------------------------------
    // UpdateProfileAsync
    // ---------------------------------------------------------------
 
    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsValidationError()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest("new@test.com", "0123456789");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.UpdateProfileAsync(userId, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("User not found");
    }
 
    [Fact]
    public async Task UpdateProfileAsync_WhenEmailTakenByAnotherUser_ReturnsValidationError()
    {
        var user = CreateUser(email: "old@test.com");
        var otherUser = CreateUser(email: "new@test.com");
        var request = new UpdateProfileRequest("new@test.com", null);
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email!))
            .ReturnsAsync(otherUser);
 
        var result = await _sut.UpdateProfileAsync(user.Id, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Email is already taken");
    }
 
    [Fact]
    public async Task UpdateProfileAsync_WhenNoChangesProvided_ReturnsValidationError()
    {
        var user = CreateUser(email: "same@test.com");
        user.PhoneNumber = "0123456789";
        var request = new UpdateProfileRequest(user.Email, user.PhoneNumber);
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
 
        var result = await _sut.UpdateProfileAsync(user.Id, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("No changes were made");
    }
 
    [Fact]
    public async Task UpdateProfileAsync_WhenEmailChangeFails_ReturnsValidationError()
    {
        var user = CreateUser(email: "old@test.com");
        var request = new UpdateProfileRequest("new@test.com", null);
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email!))
            .ReturnsAsync((User?)null);
        _userManagerMock.Setup(m => m.GenerateChangeEmailTokenAsync(user, request.Email!))
            .ReturnsAsync("email-token");
        _userManagerMock.Setup(m => m.ChangeEmailAsync(user, request.Email!, "email-token"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid email." }));
 
        var result = await _sut.UpdateProfileAsync(user.Id, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("Invalid email.");
    }
 
    [Fact]
    public async Task UpdateProfileAsync_WhenPhoneUpdateFails_ReturnsValidationError()
    {
        var user = CreateUser();
        user.PhoneNumber = "0000000000";
        var request = new UpdateProfileRequest(user.Email, "1111111111");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));
 
        var result = await _sut.UpdateProfileAsync(user.Id, request);
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("Concurrency failure.");
    }
 
    [Fact]
    public async Task UpdateProfileAsync_WhenEmailAndPhoneChange_ReturnsUpdatedUserInfo()
    {
        var user = CreateUser(email: "old@test.com");
        user.PhoneNumber = "0000000000";
        var request = new UpdateProfileRequest("new@test.com", "1111111111");
 
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email!))
            .ReturnsAsync((User?)null);
        _userManagerMock.Setup(m => m.GenerateChangeEmailTokenAsync(user, request.Email!))
            .ReturnsAsync("email-token");
        _userManagerMock.Setup(m => m.ChangeEmailAsync(user, request.Email!, "email-token"))
            .ReturnsAsync(IdentityResult.Success)
            .Callback(() => user.Email = request.Email);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.UpdateProfileAsync(user.Id, request);
 
        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("new@test.com");
        result.Value.PhoneNumber.Should().Be("1111111111");
    }
 
    // ---------------------------------------------------------------
    // UpdateProfilePictureAsync
    // ---------------------------------------------------------------
 
    private static UpdateProfilePictureRequest CreatePictureRequest(
        Stream? content = null, string contentType = "image/png") =>
        new(content ?? new MemoryStream([1, 2, 3]), contentType);
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUserNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((User?)null);
 
        var result = await _sut.UpdateProfilePictureAsync(userId, CreatePictureRequest());
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("User not found");
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenContentEmpty_ReturnsValidationError()
    {
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest(content: new MemoryStream()));
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Profile picture is required");
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenContentTypeMissing_ReturnsValidationError()
    {
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest(contentType: ""));
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Content type is required");
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenContentTypeUnsupported_ReturnsValidationError()
    {
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest(contentType: "image/gif"));
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Unsupported image type");
        _objectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenDbUpdateFails_RollsBackUploadedObject()
    {
        var user = CreateUser();
        user.ProfilePictureKey = null;
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        // The service generates the object key internally (date + random Guid),
        // so we can't hard code it. Capture whatever key was actually sent to PutAsync,
        // and reuse it below to assert the rollback deletes the *same* key.
        PutObjectRequest? capturedRequest = null;
 
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync((PutObjectRequest req, CancellationToken _) => new PutObjectResult
            {
                Bucket = req.Bucket,
                Key = req.Key
            });
 
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error." }));
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest());
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Contain("DB error.");
 
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().StartWith($"users/{user.Id}/profile-picture/");
        capturedRequest.Bucket.Should().Be("easebnb-users");
 
        // Rollback must delete the exact object that was just uploaded — even though
        // its key is randomly generated, it must be the SAME key as capturedRequest.Key.
        _objectStorageMock.Verify(
            s => s.DeleteAsync("easebnb-users", capturedRequest.Key, It.IsAny<CancellationToken>()),
            Times.Once,
            "the newly uploaded object must be rolled back when the DB update fails");
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenSucceedsWithExistingOldKey_DeletesOldObject()
    {
        var user = CreateUser();
        user.ProfilePictureKey = "users/x/profile-picture/old.png";
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResult { Key = "users/x/profile-picture/new.png", Bucket = "easebnb-users" });
 
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest());
 
        result.IsError.Should().BeFalse();
        _objectStorageMock.Verify(
            s => s.DeleteAsync("easebnb-users", "users/x/profile-picture/old.png", It.IsAny<CancellationToken>()),
            Times.Once);
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenNoOldKey_DoesNotAttemptDelete()
    {
        var user = CreateUser();
        user.ProfilePictureKey = null;
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResult { Key = "users/x/profile-picture/new.png", Bucket = "easebnb-users" });
 
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest());
 
        result.IsError.Should().BeFalse();
        _objectStorageMock.Verify(
            s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenOldObjectDeleteThrows_StillReturnsSuccess()
    {
        var user = CreateUser();
        user.ProfilePictureKey = "users/x/profile-picture/old.png";
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResult { Key = "users/x/profile-picture/new.png", Bucket = "easebnb-users" });
 
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
 
        _objectStorageMock
            .Setup(s => s.DeleteAsync("easebnb-users", "users/x/profile-picture/old.png", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("delete failed"));
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest());
 
        result.IsError.Should().BeFalse("failure to clean up the old object must not fail the request");
    }
 
    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUploadThrows_ReturnsUnexpectedError()
    {
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
 
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectStorageException(ObjectStorageErrorCode.UploadFailed, "S3 put failed"));
 
        var result = await _sut.UpdateProfilePictureAsync(user.Id, CreatePictureRequest());
 
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Failed to upload profile picture");
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<User>()), Times.Never);
    }
}