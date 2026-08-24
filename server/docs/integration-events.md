# Integration Events

How modules of the Easebnb modular monolith communicate asynchronously via
MassTransit + RabbitMQ, with a transactional outbox/inbox per module and
eventual consistency. Reference implementation: `UserRegisteredIntegrationEvent`
(Identity → Organization).

## 1. Architecture overview

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Identity module (HTTP request)                       e.g. POST /register     │
│                                                                              │
│  User aggregate                                                              │
│    └─ AddDomainEvent(UserRegisteredDomainEvent)                               │
│    └─ AddDomainEvent(SendEmailEvent)                  in-process facts        │
│                                                                              │
│  UnitOfWork.CommitTransactionAsync                                           │
│    1. DomainEventDispatcher (MediatR)                                        │
│         ├─ SendMailHandler                  internal handler runs first      │
│         └─ IntegrationEventPublisherBridge<UserRegisteredDomainEvent>        │
│              └─ IIntegrationEventMapper → UserRegisteredIntegrationEvent     │
│              └─ IPublishEndpoint.Publish  ──┐  deferred by bus outbox        │
│    2. dbContext.SaveChangesAsync           │  (rows land in the SAME        │
│       ├─ users, roles, ...                  │   transaction + schema)        │
│       └─ identity.outbox_message  ◄─────────┘                                │
│    3. COMMIT                                                                 │
│                                                                              │
│  BusOutboxDeliveryService<AppIdentityDbContext>   polls every 2s             │
└────────────────────────────────┬─────────────────────────────────────────────┘
                                 │ publish after commit
                                 ▼
                        ┌─────────────────┐
                        │    RabbitMQ     │   exchange: BuildingBlocks.IntegrationEvents
                        │  (docker-      │    .Contracts.Identity:UserRegisteredIntegrationEvent
                        │   compose)     │   queue:    user-registered
                        └────────┬────────┘   DLQ:      user-registered_error
                                 │ at-least-once delivery
                                 ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ Organization module (consumer)                                               │
│                                                                              │
│  receive endpoint "user-registered"                                          │
│    ├─ inbox check: organization.inbox_state (MessageId already consumed?)    │
│    ├─ UserRegisteredConsumer (log-only reference consumer — put real         │
│    │   application logic here)                                               │
│    └─ inbox row + any business write commit in ONE transaction               │
└──────────────────────────────────────────────────────────────────────────────┘
```

Key properties:

- **Atomic outbox**: the integration event is written into the *publishing
  module's own schema* (`identity.outbox_message`) in the same EF transaction
  as the business change. A rollback drops the event; the broker never sees
  an event for a change that did not happen.
- **Delivery after commit**: `BusOutboxDeliveryService` polls the outbox and
  publishes to RabbitMQ only once the transaction has committed.
- **At-least-once + inbox**: consumers are idempotent. The MassTransit EF
  inbox (`organization.inbox_state`) records consumed MessageIds; real
  consumers should add their own natural unique constraint as a second,
  database-level safety net.

## 2. Domain Event vs Integration Event — when to use which

| | Domain Event | Integration Event |
|---|---|---|
| Audience | The module itself | Other modules / future services |
| Transport | MediatR, in-process, synchronous | RabbitMQ, async, at-least-once |
| Lives in | Module's Core/Application project (`Easebnb.Identity.Core.Events`) | `BuildingBlocks.IntegrationEvents` (shared contracts) |
| Base type | `DomainEventBase` (`IDomainEvent : INotification`) | `IntegrationEventBase` (`IIntegrationEvent`) |
| Dispatched | `UnitOfWork` before `SaveChangesAsync` | Bus outbox after commit |
| Semantics | "Something happened inside the module; react now" | "A fact another module may need; react eventually" |
| Coupling | Free — internal | Only through the shared contracts project |

**Decision rule**: if a reaction must happen in the same transaction or only
makes sense inside the module → domain event. If another module could
someday need the fact (projections, notifications, analytics, extraction
into its own service) → also raise/propagate an integration event. Not every
domain event needs an integration event — map only the ones other modules
actually consume (`SendEmailEvent` for example stays internal).

## 3. Adding a new integration event — step by step

The reference implementation is `UserRegisteredIntegrationEvent`. To add a
new one, follow exactly these steps.

### Step 1 — Define the contract (shared project)

`BuildingBlocks/BuildingBlocks.IntegrationEvents/Contracts/<PublishingModule>/<SomethingHappened>IntegrationEvent.cs`:

```csharp
namespace BuildingBlocks.IntegrationEvents.Contracts.Booking;

public sealed record BookingPlacedIntegrationEvent : IntegrationEventBase
{
    public required Guid BookingId { get; init; }
    public required Guid GuestId { get; init; }
    public string? PromoCode { get; init; }   // optional fields: nullable, NOT required
}
```

Rules: past-tense name + `IntegrationEvent` suffix; `required` only for
fields present in v1; every field added later must be nullable and not
`required` (see §4).

### Step 2 — Raise/choose the domain event (publishing module)

There must be a domain event to map from. Either reuse an existing one or
raise a new one on the aggregate, next to the existing events:

```csharp
// in the module's Core project
public sealed class BookingPlacedDomainEvent(Guid bookingId, ...) : DomainEventBase { ... }

// where the change happens
booking.AddDomainEvent(new BookingPlacedDomainEvent(booking.Id, ...));
```

### Step 3 — Add the mapper (publishing module's Infrastructure)

`Modules/<Module>/<Module>.Infrastructure/IntegrationEvents/<Event>Mapper.cs`:

```csharp
public sealed class BookingPlacedIntegrationEventMapper
    : IIntegrationEventMapper<BookingPlacedDomainEvent>
{
    public IReadOnlyList<IIntegrationEvent> Map(BookingPlacedDomainEvent domainEvent) =>
        [new BookingPlacedIntegrationEvent { BookingId = ..., GuestId = ... }];
}
```

Register it in the module's `Add<Module>ModuleIntegrationEvents`:

```csharp
bus.TryAddTransient<IIntegrationEventMapper<BookingPlacedDomainEvent>, BookingPlacedIntegrationEventMapper>();
```

That is the entire publishing side — the MediatR bridge
(`IntegrationEventPublisherBridge<TDomainEvent>`) picks up every registered
mapper automatically. No pipeline code changes.

### Step 4 — Write the consumer (consuming module)

Two classes in the consuming module's Infrastructure, following
`UserRegisteredConsumer` as the template:

```csharp
public sealed class BookingPlacedConsumer(IBookingProjectionService projections /* ... */)
    : IConsumer<BookingPlacedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<BookingPlacedIntegrationEvent> context)
        => await projections.ApplyAsync(context.Message, context.CancellationToken);
}

public sealed class BookingPlacedConsumerDefinition : ConsumerDefinition<BookingPlacedConsumer>
{
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<BookingPlacedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
        endpointConfigurator.UseEntityFrameworkOutbox<YourModuleDbContext>(context); // inbox
    }
}
```

Register it in the module's `Add<Module>ModuleIntegrationEvents`:

```csharp
bus.AddConsumer<BookingPlacedConsumer, BookingPlacedConsumerDefinition>();
```

**Consumer rules** (idempotency is not optional):

- treat every delivery as a possible duplicate; when the consumer writes to
  the database, key the write on a natural unique constraint (e.g.
  `user_id`) so a redelivery can never duplicate a row;
- save through the module's own DbContext so the business write and the
  inbox row commit together;
- never assume ordering between integration events.

### Step 5 — Migrations (only if the module is new to outbox/inbox)

The outbox/inbox tables must exist in every participating module's schema —
`OnModelCreating` needs `modelBuilder.AddTransactionalOutboxEntities()` and a
migration (see §5). Modules that already have these tables need nothing.

### Step 6 — Run and verify

`docker compose up -d`, apply migrations, run the API with
`RabbitMq__Enabled=true`, trigger the business action, then check the broker
UI and the outbox/inbox tables (§5).

## 4. Naming & versioning conventions

- Contracts: `<FactInPastTense>IntegrationEvent` — `UserRegisteredIntegrationEvent`,
  `BookingPlacedIntegrationEvent`, `EmailConfirmedIntegrationEvent`.
- Contract records are immutable facts: `required init` properties, no
  behavior, no module types — primitives/Guids only.
- Exchange names derive from the contract's full type name
  (`BuildingBlocks.IntegrationEvents.Contracts.Identity:UserRegisteredIntegrationEvent`);
  queue names from the consumer (kebab-case: `user-registered`).
- **Versioning rule**: never rename, remove, or repurpose an existing field,
  and never add a `required` field to a released contract — new fields must
  be nullable/optional so older publishers and consumers keep working. If a
  contract must change breaking, create a new event type (e.g.
  `UserRegisteredV2IntegrationEvent`) and migrate consumers.
- Domain events: past-tense too, suffix `DomainEvent`, live in the module's
  Core project, never leave the module.

## 5. Running locally

```bash
# 1. broker (+ management UI on http://localhost:15672, guest/guest)
docker compose up -d

# 2. apply module migrations (from server/)
dotnet ef database update --project Modules/Identity/Easebnb.Identity.Infrastructure \
    --startup-project Easebnb.WebApi --context AppIdentityDbContext
dotnet ef database update --project Modules/Organization/Easebnb.Organization.Infrastructure \
    --startup-project Easebnb.WebApi --context OrganizationDbContext

# 3. run the API with the broker enabled
RabbitMq__Enabled=true dotnet run --project Easebnb.WebApi
```

RabbitMQ UI (http://localhost:15672 — guest/guest): *Queues* tab shows
`user-registered` (and `user-registered_error` after a fault); *Exchanges*
shows the fanout exchanges per contract type.

Inspecting the outbox/inbox (psql):

```sql
-- events waiting for delivery (drains within ~2s when healthy)
SELECT COUNT(*) FROM identity.outbox_message;
SELECT COUNT(*) FROM identity.outbox_state;

-- consumed messages recorded by the inbox
SELECT message_id, consumed, delivered, receive_count
FROM organization.inbox_state ORDER BY received DESC;
```

## 6. Configuration per environment

`appsettings.json` (base) + `.env` (`RABBITMQ__…`-style overrides, loaded by
DotNetEnv) — no credentials in code:

```json
"RabbitMq": {
  "Enabled": false,
  "Host": "localhost",
  "Port": 5672,
  "Username": "guest",
  "Password": "guest",
  "VirtualHost": "/"
}
```

- `Enabled: false` (default) uses the **in-memory transport**: the whole
  outbox/inbox pipeline still runs (delivery service, inbox tables) but no
  broker is needed — this is how the integration-test suite works
  (`IdentityApiFixture` pins `RabbitMq:Enabled=false`).
- Development: `RabbitMq__Enabled=true` in `.env` (or launch settings) with
  the compose broker; override `RabbitMq__Password` etc. for real brokers.
- Staging/Production: set the section via environment/deployment config;
  credentials come from the platform secret store, never from git.
- Bound and validated at startup via `IOptions<RabbitMqSettings>` +
  `ValidateOnStart` (`BuildingBlocks.Infrastructure.IntegrationEvents`).

Outbox tuning (`AddEntityFrameworkOutbox`, per module):
`QueryDelay` (poll interval — currently 2s: lower latency vs more DB load;
delivery also gets nudged by an in-process notification, so the poll is a
safety net), `QueryMessageLimit` (100/batch), `DuplicateDetectionWindow`
(how long inbox rows are kept for dedup).

## 7. Troubleshooting

**Message stuck in outbox** (`identity.outbox_message` non-empty):

1. Is the API running? The delivery service is a hosted service in the app.
2. RabbitMQ reachable? `docker compose ps`; check app logs for bus errors.
3. Row age vs `QueryDelay` — the poll only fires when the in-process
   notification missed (app restart between commit and delivery).
4. Look for delivery-service exceptions in logs, then re-run the request —
   the outbox row is still there and will be picked up.

**Message in the error queue** (`user-registered_error`):

1. Inspect the message via the management UI (*Queues → user-registered_error
   → Get Messages*) — the exception stack is in the `Exceptions` header/fault.
2. Fix the consumer bug, deploy.
3. Requeue from the UI (via the shovel plugin or re-publish the payload);
   idempotency makes re-processing safe.

**Retries**: consumers retry exponentially (5×, 1s→30s) before faulting to
the error queue. Retrying happens in memory — the message is held by the
endpoint during retries.

**Debugging idempotency issues**: check
`SELECT * FROM organization.inbox_state WHERE message_id = '…'` — if a row
exists with `consumed` set, the inbox filtered the redelivery; if the
business row duplicated anyway, the consumer wrote outside its DbContext
transaction or lacks a unique constraint.

**A published event never arrives** — verify the publisher used the concrete
type. Publishing via the base interface (`Publish<IIntegrationEvent>`) sends
to the `BuildingBlocks.IntegrationEvents:IIntegrationEvent` exchange, which
has no bindings and RabbitMQ silently drops it. The bridge always publishes
by runtime type for this reason (`IntegrationEventPublisherBridge`).

**Bus-outbox bound context**: only `AppIdentityDbContext` has
`UseBusOutbox()` (request-scope publishes). If a *new* module must publish
from its own HTTP request flow, that DbContext must own the bus outbox —
MassTransit allows exactly one per bus, so this requires either moving that
responsibility or registering a second bus. Publishing from *consumers* has
no such limit (the endpoint-level outbox covers it).

## 8. Trade-offs & known limitations

- **Outbox polling latency**: worst case ~`QueryDelay` (2s) after commit;
  usually immediate thanks to the in-process notification. Publishing
  directly to the broker (no outbox) would be faster but loses atomicity.
- **One bus outbox per bus**: request-scope publishing is bound to Identity's
  DbContext (see §7). A second module needing request-scope transactional
  publishes needs its own bus instance (`AddMassTransit<TModuleBus>`).
- **No delayed redelivery**: retry is immediate/in-memory; broker-level
  delayed redelivery needs the RabbitMQ delayed-message-exchange plugin
  (deliberately not required for local dev). Consider it before consumers
  call flaky external systems.
- **No dead-letter consumer/monitoring alert** yet: `_error` queues must be
  watched via the management UI; wire alerts into ops tooling later.
- **No saga/process-manager** yet (MassTransit sagas are available when a
  flow needs orchestration).
- **No automated tests for the new infrastructure** — deferred by scope; the
  existing integration suite covers the in-memory end-to-end path.
- **Inbox cleanup**: inbox rows are purged after the duplicate-detection
  window (30 min default, 1h for Organization) — a redelivery older than the
  window relies on the consumer's own unique constraints.
- **Tracing**: the `MassTransit` ActivitySource is wired into the existing
  OpenTelemetry setup (spans appear once `OTEL_EXPORTER_OTLP_ENDPOINT` is
  set, e.g. the Aspire dashboard); health checks for the bus are registered
  automatically by MassTransit and surface on `/health` (development).
