# AGENTS.md — Easebnb backend (`server/`)

.NET 10 modular-monolith backend for an Airbnb-like app. The git repo root is the parent `Easebnb/` directory (which also contains the `web/` frontend); this workspace covers only `server/`. Database is PostgreSQL; object storage is S3-compatible (MinIO in dev); API docs via Scalar at `/docs` in Development.

## Commands

```bash
dotnet build Easebnb.slnx                                  # build (solution is .slnx, not .sln)
dotnet test Easebnb.slnx                                   # all tests (integration tests need Docker running)
dotnet test Modules/Identity/tests/Easebnb.Identity.UnitTests                       # focused: unit
dotnet test Modules/Identity/tests/Easebnb.Identity.IntegrationTests               # focused: integration
dotnet run --project Easebnb.WebApi                        # run the API (or Aspire/Easebnb.AppHost)
RabbitMq__Enabled=true dotnet run --project Easebnb.WebApi # run with the RabbitMQ integration-event bus
docker compose up -d                                       # RabbitMQ (management UI: localhost:15672, guest/guest)
pwsh scripts/new-module.ps1 -ModuleName Foo -BaseNamespace Easebnb   # scaffold a new module
```

- Integration tests spin up PostgreSQL via Testcontainers (`postgres:16-alpine`) — Docker must be running.
- Tests are xUnit + FluentAssertions + Moq; test projects reference `BuildingBlocks.Tests.Base` for those packages. Prefer constructor-injected fixture classes (see `IdentityModuleTestBase`/`IdentityModuleFixture`).
- EF Core migrations live in each module's `Infrastructure/Database/Migrations` (provider: Npgsql; `dotnet ef` commands run from the WebApi project, which has the Tools package and UserSecrets). Integration events add outbox/inbox tables per module — new modules need `modelBuilder.AddTransactionalOutboxEntities()` in `OnModelCreating` plus a migration.

## Layout

- `Easebnb.WebApi/` — the single host. Feature endpoints live in `Modules/<Module>/<Feature>/<Action>Endpoint.cs` (e.g. `Modules/Identity/Auth/LoginEndpoint.cs`). `Program.cs` is the composition root that calls each module's registration.
- `Modules/<Module>/` — `Domain` / `Application` / `Infrastructure` projects per module. `Identity` and `Organization` are implemented; `Modules/Organization/` is the minimal reference consumer (no Domain project — mirrors Identity's live layout).
- `BuildingBlocks/` — cross-module libraries: `Application` (abstractions: email, object storage, queries), `Infrastructure` (S3, domain events, file upload, integration-event bus & MediatR bridge), `IntegrationEvents` (shared integration-event contracts — zero dependencies, pure records), `Endpoints` (endpoint discovery), `SharedKernel`, `Utils`.
- `Database/Easebnb.Database/` — EF Core plumbing shared by modules: `AddDatabase<TDbContext>(moduleName)` (per-module schema), `AuditableEntityInterceptor`, snake_case naming via EFCore.NamingConventions.
- `Aspire/Easebnb.AppHost/` — minimal Aspire host (currently just launches web-api).
- `docker-compose.yml` — local RabbitMQ for integration events.
- `docs/integration-events.md` — integration-event architecture, how to add events/consumers, troubleshooting.
- `scripts/new-module.ps1` — scaffolds Domain/Application/Infrastructure, wires references, adds to .slnx.

## Architecture rules

- Module layering: `Domain` ← `Application` ← `Infrastructure`; modules may reference `BuildingBlocks/*` but never each other.
- A module registers itself via an `Add<Module>Module(IConfiguration)` extension in its Infrastructure project (see `InfrastructureModule.AddIdentityModule`), called from `Easebnb.WebApi/Program.cs`. The code uses C# `extension` members (C# 14) for this — follow that style.
- Each module owns its DbContext (`AppIdentityDbContext`) and DB schema; register with `services.AddDatabase<TDbContext>("ModuleName")`.
- HTTP endpoints implement `IEndpoint`/`IEndpointGroup` and are auto-discovered via `RegisterEndpointsFromAssemblyContaining<T>()` + `app.MapEndpoints()` — do not hand-register routes.
- Domain events dispatched via `IDomainEventDispatcher`/`IDomainEventsAccessor` after save.
- Cross-module communication goes through integration events only: contracts in `BuildingBlocks.IntegrationEvents`, mapped from domain events by one `IIntegrationEventMapper<TDomainEvent>` class per mapping, consumed by `IConsumer<T>` + `ConsumerDefinition<T>` (inbox) in the other module. Each module registers via `Add<Module>ModuleIntegrationEvents(cfg, configuration)` inside the single `AddMassTransit` block in Program.cs. Adding a mapping or consumer must never require changes outside the module (plus one registration line) — see `docs/integration-events.md`.
- `RabbitMq:Enabled` (appsettings/.env) toggles RabbitMQ vs in-memory transport; the outbox/inbox pipeline is always active.
- `Easebnb.WebApi/.env` is loaded at startup by DotNetEnv (`Env.Load()` in Program.cs); dev connection strings/keys also sit in `Easebnb.WebApi/appsettings.json`.
- Serilog is configured in `appsettings.json` (`Serilog` section); request logging and TraceId enrichment are set up in Program.cs — don't add per-controller logging plumbing.

## Naming gotchas (important)

- `Modules/Identity/Easebnb.Identity.Application/` is a directory whose project file is `Easebnb.Identity.Core.csproj` with namespaces `Easebnb.Identity.Core.*`. The "Application" folder and "Core" project name disagree — always check the actual csproj name when referencing it.
- `Modules/Identity/Easebnb.Identity.Domain/` is **orphaned**: not in `Easebnb.slnx`, referenced by nothing, uses legacy `TmsBase.Identity.Domain.*` namespaces. Do not add code to it; live entities are `Easebnb.Identity.Core.Entities` inside the Application/Core project.
- `BuildingBlocks/BuildingBlocks.Utils/` uses namespaces `Dayline.BuildingBlocks.Utils.*` (another legacy prefix).
- `scripts/new-module.ps1` defaults to `-BaseNamespace TmsBase` — pass `-BaseNamespace Easebnb` explicitly.

## Conventions

- File-scoped namespaces, 4-space indent, `Nullable` and `ImplicitUsings` enabled everywhere; target framework `net10.0` on every project.
- No `Directory.Build.props`, `Directory.Packages.props`, or `.editorconfig` — package versions are pinned per csproj.
- Branch flow: work happens on `develop`; PRs target `master`.
