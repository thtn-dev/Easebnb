# Plan — Organization module integration tests

New project `Modules/Organization/tests/Easebnb.Organization.IntegrationTests`, collection `"OrganizationApi"` (own host + container). Files: csproj, GlobalUsing, TestJwtKeys, FakeSendEmailHandler (copies), OrganizationApiFixture (mirror IdentityApiFixture), OrganizationApiTestBase (identity helpers + org helpers: CreateOrganizationAsync, SeedRegisteredUserAsync, SeedMembershipAsync, GetOrganizationFromDbAsync, GetMembershipFromDbAsync, WaitForRegisteredUserAsync, SetupSuccessfulLogoUpload, JpegBytes via ImageSharp), 4 test files.

## Phases → checklist mapping

### Phase 1 — scaffolding + smoke (items 13, 14; item 1 partially)
Fixture + base + `Organizations/OrganizationEndpointTests` first test (Create happy). Docker run green before proceeding.

### Phase 2 — org endpoints (items 1–7)
- `Organizations/OrganizationEndpointTests.cs`: Create (6), Get (4), My orgs (2), Update (4), Archive (3) ≈ 19 tests.
- `Organizations/OrganizationLogoEndpointTests.cs`: 4 tests (happy incl. replace-old-logo + S3 verifies, non-image 400, member 403, 401). ≤4 handler-reaching uploads (rate limiter global 10/min).

### Phase 3 — member endpoints (items 8–11)
- `Organizations/OrganizationMemberEndpointTests.cs`: GetMembers (3), AddMember (6), ChangeRole (5), RemoveMember (3) ≈ 17 tests. Transfer test asserts DB: target Owner, old owner Admin, owner_user_id moved.

### Phase 4 — cross-module E2E (item 12)
- `IntegrationEvents/UserRegisteredProjectionTests.cs`: 1 comprehensive test — register → WaitForRegisteredUserAsync (≤15s) → assert projection content → AddMember 200 → GetMembers contains real DisplayName/Email. (Plan's "second test" assertions folded into this one; recorded in status.md.)

### Verification (items 13–15)
- Narrow: `dotnet test Modules/Organization/tests/Easebnb.Organization.IntegrationTests` exit 0.
- Workspace: `dotnet build Easebnb.slnx`; full `dotnet test Easebnb.slnx` (318 existing + new).
- Checklist review → `.testagent/status.md` + Requirement|Evidence table.

## Risk mitigations

- Async projection: only E2E test polls; all other tests seed registered_users directly.
- Rate limiter: ≤4 handler-reaching uploads across suite.
- TRUNCATE during in-flight delivery: lock_timeout guard; phantom projections can't collide (random ids).
- Entity/namespace collision: namespace-scoped `using Organization = ...` alias where the type is named.
