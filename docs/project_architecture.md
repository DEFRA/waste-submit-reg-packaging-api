# Architecture: modules in one service (skeleton, for discussion)

This branch (`spike/modular-skeleton`) shows the structure proposed for `waste-submit-reg-packaging-api`.
Two pieces of work will share the service:

- **Packaging.** The packaging workflow, being written from scratch now.
- **Registration.** The producer and compliance scheme registration workflow, moving here later from the
  Azure services (epr-payment-service and others).

The aim is for both to be built the same way, so Registration drops in as one more module without any
restructuring. The Registration and Packaging code here is **sample code**: just enough to show each pattern
working end to end, with tests. It is not the real domain.

## Layout

```
WasteSubmitRegPackagingApi/                      Host: Program.cs, CDP plumbing (auth, logging, Postgres, trust store).
                                                 Wires up modules; holds no business logic.
WasteSubmitRegPackaging.SharedKernel/            Result/Error, ICommand/IQuery + handlers, AggregateRoot, IDomainEvent, Actor.
                                                 No dependencies at all.
WasteSubmitRegPackaging.SharedKernel.Infrastructure/
                                                 ModuleDbContext, outbox (interceptor + dispatcher), Result → ProblemDetails,
                                                 actor headers, handler registration.
WasteSubmitRegPackaging.Registration.Contracts/  What Registration offers other modules (IRegistrationModuleApi + DTOs).
WasteSubmitRegPackaging.Registration/            The Registration module.
WasteSubmitRegPackaging.Packaging/               The Packaging module.
changelog/                                       Liquibase: db.changelog.xml → changes/<module>/NNNN-*.xml
WasteSubmitRegPackagingApi.Test/Modules/         Architecture, domain, handler, endpoint and database tests.
```

Inside each module, folders are layers:

```
Domain/          Aggregates, state machines, domain events, errors. No EF Core, ASP.NET Core or Npgsql.
Application/     One folder per use case: Commands/<UseCase>/ and Queries/<UseCase>/ (command or query + handler).
Infrastructure/  The module's DbContext, EF mappings, repositories, and the implementation of its Contracts.
Endpoints/       Minimal API routes and request types. Thin.
<Name>Module.cs  The only public entry point: Add<Name>Module() and Map<Name>Endpoints().
```

## Dependency rules (enforced by `ArchitectureTest`)

- Modules never reference each other. Packaging may reference `Registration.Contracts`, never
  `Registration`. The project references make this a compile error, not just a review comment.
- `Domain` doesn't depend on EF Core, ASP.NET Core, Npgsql or any other layer of its module.
- `SharedKernel` has no infrastructure dependencies.
- Only `<Name>Module`, `*Request` types and the enums they use are public. Everything else is `internal`.
- Each module owns its tables (`reg_*`, `pkg_*`). **No joins or foreign keys across modules.** Store the other
  module's ID and ask through its Contracts.

## Anatomy of a use case

Take *"a regulator grants a registration"* as the example: `POST /regulator/registrations/{id}/decisions`.

1. **Endpoint** (`Endpoints/RegistrationEndpoints.cs`). Binds the request, builds the command with the actor,
   calls the handler, and maps the `Result`.
2. **Command and handler** (`Application/Commands/RecordRegulatorDecision/`). Checks application rules (who
   may do this, which inputs are required), loads the aggregate through the repository, calls the domain
   method, saves.
3. **Aggregate** (`Domain/RegistrationSubmission.cs`). Owns the state machine. `RecordDecision` either changes
   state, adds a history row and raises `RegistrationDecisionRecorded`, or returns a Conflict error.
4. **Save** (`Infrastructure/RegistrationSubmissionRepository.cs`). One `SaveChanges` writes the aggregate, its
   history and its outbox message, all in one transaction.

To add a use case: create a folder under `Application/Commands` or `Application/Queries`, add the
command or query record and its handler, then add a route in `Endpoints`. Handlers are registered
automatically by `AddHandlersFromAssembly`. There is no mediator library; endpoints ask DI for
`ICommandHandler<TCommand, TResult>` directly.

## Validation happens in three places

| Where | What | Result |
|---|---|---|
| Request type (DataAnnotations) | Shape: required, lengths, ranges. Checked by .NET 10 `AddValidation()` before the endpoint runs | 400 `ValidationProblem` with `errors` |
| Handler | Application rules: actor type, own organisation, required-when rules, anything that needs the database | 400 or 403 `ProblemDetails` |
| Aggregate | Invariants: is this transition allowed now? | 409 `ProblemDetails` |

Expected failures are returned as `Result`/`Error` values, never thrown. Every error has a stable `code`
(for example `registration.decision_not_allowed`) in the ProblemDetails, so the frontends can switch on it.
Exceptions are kept for bugs and infrastructure faults, and become a 500.

> **Gotcha found while building this:** the .NET 10 validation source generator silently **skips internal
> request types**. The request then reaches the handler unvalidated. That's why request types are public;
> `ArchitectureTest` checks it.

## Commands, queries and state

- Commands go through the aggregate and a repository. Queries read the module's DbContext with
  `AsNoTracking()` and project into view models (see `GetPackagingSubmission`).
- **State is stored, not recalculated.** The aggregate keeps its current `Status`, and history is an append-only
  table. That's the main change from today's Cosmos/Synapse model, where every read replays events, and that
  logic exists in five places that disagree.
- **The backend returns ready-to-render state.** For example, `AvailableRegulatorDecisions` tells the
  regulator UI which buttons to show, so the frontends don't re-derive rules.

## Persistence

- **Liquibase owns the schema.** The service runs as CDP's DML-only user, so EF never migrates. Locally,
  `compose.yml` mirrors this: `compose/postgres/init.sql` creates a DDL user and a DML-only app user, and a
  `liquibase` service applies `changelog/` before the app starts.
- **Changesets are never edited** once applied, because Liquibase checksums them. Ids are
  `<module>-<NNNN>-<what>`, so the two teams can't clash.
- **Naming.** EF uses the snake_case convention (`ApplicationReferenceNumber` → `application_reference_number`).
  Table names are set explicitly with the module prefix. `ModuleSchemaTest` applies the real changelog to
  Postgres and round-trips an aggregate, which proves the mapping and changelog agree.
- **IDs** are strongly typed (`RegistrationSubmissionId`) and created in code with `Guid.CreateVersion7()`.
- **Time** is `DateTimeOffset` UTC from an injected `TimeProvider`, so tests use a fixed clock.
- **Enums** are stored as text with CHECK constraints, not PG enum types. The exception is a value that has
  an id in a shared reference table, which is stored as that id: `Nation` is stored as `nation_id` (1 GB-ENG,
  2 GB-SCT, 3 GB-WLS, 4 GB-NIR) so data carried over from Azure maps straight across. The API uses the codes
  (`GB-ENG`) and also accepts the ids.
- **Concurrency.** Postgres `xmin` is used as a row version, so two regulators deciding at once gives one success and
  one 409 (`registration.concurrent_change`).
- **Unique clashes** are recognised from the Postgres error code (`23505`), never by matching message text.

## Outbox

Domain events are written to the module's outbox table (`reg_outbox_message`, `pkg_outbox_message`) by
`DomainEventsToOutboxInterceptor`, in the same transaction as the change. `OutboxDispatcher<TContext>`
polls with `FOR UPDATE SKIP LOCKED`, so several instances are safe, and passes each message to an
`IOutboxMessagePublisher`. The skeleton's only publisher logs. Real ones would be GOV.UK Notify, an
SNS/SQS feed, or the downstream registration feed. The dispatcher is off unless `Outbox:DispatcherEnabled` is
true. Delivery is at-least-once, so publishers must cope with duplicates.

## Calling another module

Packaging needs to know whether an organisation is registered. It calls
`IRegistrationModuleApi.GetLatestRegistrationAsync`, defined in `Registration.Contracts` and implemented inside
Registration. The contract has its own `RegistrationState` enum, separate from the domain's, so Registration
can change its internals freely. If a module is ever split into its own service, the contract becomes an
HTTP client and callers don't change.

## Security and the acting user

- **Services** are authenticated by the existing ACL (Basic API key per client). Queries need the `Read` policy
  (scope `read`) and commands need `Write` (scope `write`, added on this branch).
- **The acting user.** Commands get it from `X-Actor-User-Id`, `X-Actor-Type` and optionally
  `X-Actor-Organisation-Id`, enforced by `.RequireActor()`. **This is a placeholder:** see the open questions.

## Tests

| Kind | Example | Needs |
|---|---|---|
| Architecture | `ArchitectureTest` | Nothing |
| Domain (most rule tests belong here) | `RegistrationSubmissionTest` | Nothing |
| Handler | `RecordRegulatorDecisionHandlerTest`, `CreatePackagingSubmissionHandlerTest` (NSubstitute) | Nothing |
| Endpoint (real host, in-memory repository) | `RegistrationEndpointsTest` | Nothing |
| Database (Liquibase + EF on Postgres) | `ModuleSchemaTest` | Docker; skipped without it, as in the Dockerfile build |

## For discussion

1. **The acting user.** Are trusted headers from allow-listed services enough, or should the user's Defra ID
   token, or a signed actor token, be passed through?
2. **Projects or folders per module.** Projects give compile-time boundaries but more `.csproj` files. Is that
   worth it?
3. **Repository or DbContext in command handlers.** The skeleton uses a small repository, to keep handlers
   testable with NSubstitute and keep EF out of the domain.
4. **The mediator.** None here. Do we want pipeline behaviours (logging, transactions) as decorators?
5. **Outbox transport.** Notify directly, SNS/SQS, or both? Who consumes registration events downstream (public
   register, PayCal)?
6. **Read models.** For the paged, filtered regulator list, use projections over the aggregate tables, or a
   dedicated read table?
7. **The existing obligations endpoints** live in the host. Should they move into the Packaging module?
8. **Contract enums vs domain enums.** Request types currently use domain enums, so those must be public. Should
   they get their own API enums instead?
