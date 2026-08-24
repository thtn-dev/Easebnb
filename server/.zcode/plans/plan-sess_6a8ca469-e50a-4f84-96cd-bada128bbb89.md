# Integration Test Plan — Identity Module (HTTP endpoint level)

## Quyết định đã chốt
- Tầng test: **HTTP endpoint** qua `WebApplicationFactory<Program>` + Testcontainers Postgres.
- Gộp **1 fixture WAF duy nhất**; test service-level cũ (`AccountServiceTests`) chuyển sang dùng `factory.Services`.
- Cho phép thêm 1 dòng `public partial class Program;` vào `Easebnb.WebApi\Program.cs` (thay đổi production duy nhất).

## Setup
1. `Easebnb.WebApi\Program.cs`: thêm `public partial class Program;` cuối file.
2. `Easebnb.Identity.IntegrationTests.csproj`: thêm `Microsoft.AspNetCore.Mvc.Testing` (10.x) + `ProjectReference` → `Easebnb.WebApi`.

## Hạ tầng test (files mới trong Easebnb.Identity.IntegrationTests)
- **`TestJwtKeys.cs`** — sinh cặp RSA-2048 1 lần/lần chạy, export PEM (PKCS#1 private + SPKI public, đúng format `.env` đang dùng), Issuer/Audience `easebnb-test`.
- **`FakeSendEmailHandler.cs`** — thay `SendMailHandler` (đang `Task.Delay(10000)`) bằng `INotificationHandler<SendEmailEvent>` ghi lại event vào list thread-safe, trả về ngay. Register không còn chậm 10s; đồng thời cho phép assert "email đã được phát sinh".
- **`IdentityApiFixture.cs`** — `WebApplicationFactory<Program>` + `IAsyncLifetime`: start `postgres:16-alpine`; `ConfigureAppConfiguration` override `Database:ConnectionString` (container) + `Jwt:PrivateKey/PublicKey/Issuer/Audience` (TestJwtKeys); `ConfigureServices`: `RemoveAll<IObjectStorage>` → Moq mock dùng chung, `RemoveAll<INotificationHandler<SendEmailEvent>>` → FakeSendEmailHandler; chạy `MigrateAsync()` qua scope. Lưu ý: `Env.Load()` không tìm thấy `.env` ở cwd test → cấu hình in-memory đảm nhiệm toàn bộ Jwt.
- **`IdentityApiTestBase.cs`** — collection `IdentityApi`; expose `HttpClient`, scope `DbContext`/`UserManager` (arrange dữ liệu); helper: `RegisterUserAsync`, `LoginAsync` (parse envelope `ApiResponse<LoginResponse>`), client có Bearer token, sinh token reset/confirm qua `UserManager` + `WebEncoders.Base64UrlEncode`; **cleanup mỗi test**: `TRUNCATE identity.users, identity.refresh_tokens, identity.roles, identity.user_roles, identity.user_claims, identity.user_logins, identity.user_tokens, identity.role_claims RESTART IDENTITY CASCADE` (giải quyết TODO respawn hiện tại).
- **`AccountServiceTests.cs`** (sửa) — giữ test hiện có, chuyển sang fixture mới (`factory.Services` resolve `IAccountService`), xóa `IdentityModuleFixture`/`IdentityModuleTestBase` cũ.

## Test files (~46 test, đặt trong thư mục `Auth/` và `Account/`)

**Auth**: `RegisterEndpointTests` (204 + user persist chưa confirm + email event ghi nhận; trùng username 409; trùng email 409; password mismatch 400 ProblemDetails; body rỗng 400 ValidationProblem), `LoginEndpointTests` (200 envelope đúng trường `accessToken/refreshToken/tokenType=Bearer/expiresIn=3600/user`; sai user/mật khẩu 401; khóa tài khoản sau 5 lần sai 403; empty 400 ValidationProblem; access token thu được dùng được cho endpoint có `[Authorize]`), `RefreshTokenEndpointTests` (rotate 200 + old token revoked/ReplacedByToken trong DB; unknown 401; revoked 401; expired 401 — arrange set `ExpiresAt`; empty 400; **body dùng property `refreshToken` — Google DTO**), `RevokeTokenEndpointTests` (204 + IsRevoked trong DB; unknown 404; đã revoke 404), `LogoutEndpointTests` (200 + revoke toàn bộ refresh token của user; không token 401), `JwksEndpointTests` (kid = 16 ký tự đầu Base64(SHA256(public key)), kty RSA, alg PS256).

**Account**: `ChangePasswordEndpointTests` (204 + mật khẩu cũ không login được/mới login được; sai current 400; mismatch 400; không token 401), `ConfirmEmailEndpointTests` (token hợp lệ 204 + `EmailConfirmed`; token sai 400), `ForgotPasswordEndpointTests` (email tồn tại 204; email không tồn tại vẫn 204 — anti-enumeration), `ResetPasswordEndpointTests` (token hợp lệ 204 + login bằng mật khẩu mới 200; token sai 400; mismatch 400), `GetCurrentUserEndpointTests` (200 envelope UserInfo đủ trường; không token 401), `UpdateProfileEndpointTests` (đổi phone/email 200 + persist trong DB; không thay đổi gì 400; email sai format 400 ValidationProblem), `ResendEmailConfirmationEndpointTests` (chưa confirm 204 + event; đã confirm 400; email lạ 204), `ChangePictureProfileEndpointTests` (PNG 1×1 thật qua multipart field `file` → 204 + `ObjectStorageMock.Verify(PutAsync, bucket "easebnb-users")`; content không phải ảnh 400 `{error}`; không token 401).

Các điểm sẽ xác minh lại khi viết (ghi chú từ khảo sát): error type chính xác của ConfirmEmail/ResetPassword (400 hay 404), body `refreshToken` của Google DTO, response `{"error":...}` thô của ChangePictureProfile.

## Convention
Giữ pattern hiện có: xUnit collection fixture + primary constructor, FluentAssertions, `Method_Condition_ExpectedResult` (kiểu IntegrationTests cũ), AAA comment đầy đủ, global usings có sẵn FluentAssertions/Moq.

## Verification
1. Build test project, chạy `dotnet test` riêng project IntegrationTests (cần Docker chạy) — kỳ vọng exit 0, toàn bộ pass.
2. `dotnet build Easebnb.slnx` toàn solution.
3. Cập nhật `.testagent/plan.md`, `research.md`, `status.md` cho task này; báo cáo cuối dạng `Requirement | Evidence`.