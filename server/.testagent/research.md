# Research — Organization module integration tests (Broad scope)

Date: 2026-08-23 · Workspace: `server/` (.NET 10) · Branch: `feature/org-module`

## Scope

HTTP integration tests for the Org module's 11 endpoints under `/api/v1/organizations` in `Easebnb.WebApi/Modules/Organization/`, plus one cross-module E2E test for the user-registration projection pipeline. New project `Modules/Organization/tests/Easebnb.Organization.IntegrationTests`, mirroring `Easebnb.Identity.IntegrationTests`.

## Conventions discovered (from Identity.IntegrationTests — the precedent)

- Fixture: `WebApplicationFactory<Program>` + `PostgreSqlContainer` (postgres:16-alpine), env "Testing", in-memory config overrides (DB conn, `RabbitMq:Enabled=false` → in-memory transport with full outbox/inbox pipeline, JWT test keys), `RemoveAll<IObjectStorage>` → shared `Mock<IObjectStorage>`, `RemoveAll<INotificationHandler<SendEmailEvent>>` → `FakeSendEmailHandler` (real handler delays 10s), migrate BOTH `AppIdentityDbContext` and `OrganizationDbContext`, seed "user" role.
- Base: `[Collection(...)]` + `ICollectionFixture`; primary-ctor base `(fixture) : IAsyncLifetime`; per-test cleanup = `ExecuteSqlRawAsync("SET lock_timeout = '5s'; TRUNCATE ...")` + reset mock invocations; helpers `RegisterUserAsync`/`LoginAsync`/`CreateAuthorizedClientAsync`/`LoginAsAsync` (register → login → Bearer client), `PostJsonAsync`, `ReadJsonAsync`.
- Test style: class-per-endpoint-group with Arrange/Act/Assert comments; anonymous camelCase JSON bodies; status-code asserts + `JsonElement` body checks (`detail` for ProblemDetails, `errors` PascalCase keys for ValidationProblem); typed envelopes via `ReadFromJsonAsync<ApiResponse<T>>`.
- csproj: Mvc.Testing 10.0.10, Testcontainers.PostgreSql 4.14.0, Microsoft.Extensions.Hosting 10.0.10, xunit 2.9.3, Test.Sdk 17.14.1, coverlet 6.0.4; refs BuildingBlocks.Tests.Base + module Core/Infrastructure + Easebnb.WebApi. GlobalUsing: FluentAssertions + Moq.
- Org-specific facts: success envelope is `ApiResponse<T>` (`{success,message,data}`) for non-paged endpoints; **paged endpoints return bare `PaginatedResponse<T>`** (`{success,data:{items,pagination}}`, no ApiResponse wrapper); validation keys PascalCase (`Slug`, `Name`, `UserId`, `Role`); ProblemDetails by ErrorType (400/401/403/404/409/500) with `traceId`; "file-upload" rate limiter is a **global** fixed window 10 req/min (auth 401/403 blocked before limiter → only handler-reaching uploads count); entity `Organization` name collides with `Easebnb.Organization` namespace → namespace-scoped using alias in files naming the type; no DB FK across schemas → TRUNCATE of both schemas safe; projection latency ≈ MassTransit outbox QueryDelay (2s) → poll helper with 15s timeout.

## Acceptance checklist

1. Create org via HTTP: 200 envelope + DB invariants (org Active, owner_user_id = creator, Owner membership).
2. Create: slug auto-generation from Vietnamese name; 401 unauthenticated; 400 invalid slug (ValidationProblem key `Slug`); 400 missing name; 409 slug taken.
3. Get by id: 200 member / 403 non-member / 404 unknown; get by slug with different casing 200.
4. My organizations: only caller's orgs + pagination metadata; page=0 → 400.
5. Update: owner 200 + persisted; 409 slug taken; 403 member; 409 archived org.
6. Archive: owner 204 + DB Archived + blocks later update (409); admin 403.
7. Logo upload: valid JPEG 200 + persisted key + S3 mock verified (bucket, key prefix, old key deleted); non-image 400; member 403; unauthenticated 401.
8. GetMembers: enriched list from registered_users (null display fields without projection), pagination metadata; non-member 403.
9. AddMember: 200 enriched response; 404 unknown user; 409 duplicate; 409 role Owner; 400 invalid role; 403 Member actor.
10. ChangeRole: ownership transfer 200 with all three DB asserts; admin promote 200; admin grants Owner 403; changing current owner's role 409; archived org 409.
11. RemoveMember: owner 204 + removal visible; target owner 409; admin removes admin 403.
12. Cross-module E2E: register via /api/v1/auth/register → projection appears in organization.registered_users (poll ≤15s, correct email/username) → AddMember for that user returns 200 → GetMembers shows real display info (no manual seeding).
13. Per-test isolation: TRUNCATE identity + organization tables (incl. outbox/inbox), roles kept, mock invocations cleared.
14. Scaffolding mirrors Identity.IntegrationTests; project added to Easebnb.slnx.
15. New suite passes (Docker); full solution unregressed.

## Commands

- `dotnet test Modules/Organization/tests/Easebnb.Organization.IntegrationTests` (narrow, Docker required)
- `dotnet build Easebnb.slnx`
- `dotnet test Easebnb.slnx` (full)
