# Status — Organization module integration tests

Date: 2026-08-23 · Branch: `feature/org-module` · Docker required (Testcontainers Postgres 16-alpine)

## What was built

New test project `Modules/Organization/tests/Easebnb.Organization.IntegrationTests` (41 tests, 4 test files), registered in `Easebnb.slnx`. Mirrors `Easebnb.Identity.IntegrationTests`: `OrganizationApiFixture` (WebApplicationFactory<Program> + Postgres Testcontainer + in-memory config overrides + ObjectStorage mock + FakeSendEmailHandler, both DbContexts migrated, "user" role seeded), `OrganizationApiTestBase` (collection `"OrganizationApi"`, register/login/Bearer-client helpers, per-test TRUNCATE of identity + organization schemas incl. outbox/inbox, org helpers: CreateOrganizationAsync / SeedMembershipAsync / SeedRegisteredUserAsync / DB getters / WaitForRegisteredUserAsync poll / logo-upload helpers).

## Files

| File | Tests |
|---|---|
| Organizations/OrganizationEndpointTests.cs | 18 |
| Organizations/OrganizationLogoEndpointTests.cs | 4 |
| Organizations/OrganizationMemberEndpointTests.cs | 16 |
| IntegrationEvents/UserRegisteredProjectionTests.cs | 3 |

Total: 41 (all passing).

## Production-code changes

None. (Previous InternalsVisibleTo from the unit-test run is still in place but not used by this suite.)

## Issues found during development

- `RegisterUser_WhenProjectionConsumed_InboxRecordsTheMessageId` initially failed with `InvalidOperationException: Connection is not open` — the raw ADO scalar query ran on a connection that the pooled DbContext had not opened. Fixed in the test with `db.Database.OpenConnectionAsync()` before executing. Test-only fix; production untouched.
- Rate-limiter budget confirmed safe: only 3 handler-reaching uploads across the suite (global fixed window 10/min); auth-blocked 401/403 requests never reach the limiter.

## Assertion-quality review notes

- Every happy path asserts both the HTTP envelope and the database state (owner membership, role changes, owner_user_id transfer, logo key, archived status).
- Negative paths assert state was NOT mutated (e.g. owner role kept after demote attempt, membership absent after forbidden add).
- The E2E projection test asserts the full chain end state (projection content + add-member succeeds + member list shows real display info + new member can access the org) and a second test proves the inbox pipeline recorded consumed MessageIds.
- Plan's "second projection test" (GetMembers display info) was folded into the flagship E2E test plus the inbox-state test — both aspects have dedicated assertions.

## Verification results (executed, clean exits)

| Command | Result |
|---|---|
| `dotnet test Modules/Organization/tests/Easebnb.Organization.IntegrationTests` | **Passed 41/41, exit 0** (~13 s, Docker) |
| `dotnet build Easebnb.slnx` | **Build succeeded, 0 errors** |
| `dotnet test Easebnb.slnx` | **359/359 passed** (89 BuildingBlocks + 103 Organization unit + 76 Identity unit + 50 Identity integration + 41 Organization integration), exit 0 |
