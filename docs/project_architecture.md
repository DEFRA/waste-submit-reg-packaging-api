# Architecture: modules in one service (skeleton, for discussion)

This branch (`spike/modular-skeleton`) shows the structure proposed for `waste-submit-reg-packaging-api`.
The service is split by business domain, into four modules:

- **Packaging.** The packaging workflow, being written from scratch now.
- **Registration.** The producer and compliance scheme registration workflow, moving here later from the
  Azure services (epr-payment-service and others).
- **Fees.** The fee tables (rates by fee type, regulator and date period) and the fee calculations, shared by both
  workflows.
- **SubmissionPeriods.** Registration submission periods and packaging submission periods, each packaging period
  linked to one registration period, shared by both workflows.

See [Shared modules: Fees and SubmissionPeriods](#shared-modules-fees-and-submissionperiods).

The aim is for the workflows to be built the same way, so Registration drops in as one more module without any
restructuring. The Registration and Packaging code here is **sample code**: just enough to show each pattern
working end to end, with tests. It is not the real domain. The registration, regulator and packaging submission
endpoints (`/registrations`, `/regulator/registrations`, `/packaging/submissions`) are marked **(sample)** with
`.AsSample(...)`, which adds a "SAMPLE:" description and keeps them out of the v1 OpenAPI document. They're in the
`unpublished` one (see the README, "Publishing an endpoint"). They give the structure now and will be developed in due
course, so no frontend or service should build against them yet. The Manage Obligations endpoints (`/packaging/{packagingYear}/...`)
are real and aren't marked. `OpenApiTest` checks both. Fees and SubmissionPeriods are **real**: their tables,
data and the resubmission fee calculation are ported from epr-payment-service and today's config, with parity tests.

The patterns are standard .NET ones. See [Where the patterns come from](#where-the-patterns-come-from).

## How it works (start here)

The service is a plain HTTP API. Its callers are **both frontends and other services**:

| Caller | Kind | Typical use |
|---|---|---|
| waste-submit-reg-packaging-fe | Frontend | Producer and compliance scheme screens: start and submit a registration, submit packaging data |
| waste-packaging-regulators-fe | Frontend | Regulator screens: review, grant, refuse, query, cancel |
| Manage Obligations (waste-obligations) | **Service** | Packaging data with the registration it relates to, for obligation calculations. The existing `/packaging/{year}/…` endpoints already serve it |
| Others to come (e.g. PayCal, public register, reporting) | **Service** | Read-only questions, usually across packaging **and** registration |

Every caller is a client in the ACL with its own key and scopes (see [Security](#security-and-the-acting-user)).
Service callers usually only need `read`.

It does three jobs:

1. **Answers questions** (GET): data for screens, and data for other services, including questions that
   combine packaging and registration (see [Questions that combine domains](#questions-that-combine-domains)).
2. **Accepts creates and updates** (POST/PUT).
3. **Publishes an event when something important happens in the workflow**, for example when a regulator
   grants a registration or accepts a packaging submission. Other services consume these events.

Other services can either **ask** (call a GET endpoint when they need the data) or **listen** (consume
events to keep their own copy up to date). Asking is the default. Events are for when a service needs to react
straight away, or would otherwise poll.

### A read (GET)

```
Caller ──GET──▶ Endpoint ──▶ Query handler ──▶ DbContext (AsNoTracking) ──▶ view model ──▶ JSON
(frontend
 or service)
```

A read is a direct database query shaped for its caller. It doesn't load an aggregate, run business rules or
raise events. For a screen, the view model includes things the UI would otherwise have to work out itself,
such as the current status and which actions are available. For a service, it's the data that service needs
in one call, so the service doesn't stitch several calls together.

### A write (POST/PUT)

```
Caller ──POST──▶ Endpoint ──▶ Command handler ──▶ load entity ──▶ entity method ──▶ SaveChanges ──▶ 200/201
                 (shape +      (who may do it,     (repository)    (is this allowed    (entity + history
                  user check)   needs DB?)                          now? change it)     + outbox, one txn)
```

### Publishing an event

```
entity method calls Raise(event)
        │
        ▼  same SaveChanges / same transaction
outbox table row (reg_/pkg_outbox_message)  ──▶  OutboxDispatcher (background, polls)  ──▶  IOutboxMessagePublisher  ──▶  SNS/SQS, Notify, …
```

The event is saved in the **same transaction** as the change. If the change is saved, the event will be
sent, even if the message broker is down at that moment. If the save fails, no event exists.

### The terms, in plain English

| Term | What it means here |
|---|---|
| **Aggregate** | The main entity for a workflow (`RegistrationSubmission`, `PackagingSubmission`) together with its child rows (e.g. history). It's an ordinary EF entity that is loaded and saved as one unit. Every change to its status goes through its methods. |
| **State machine** | The entity's `Status` column plus the rule for which status changes are allowed. It's a `switch` in the entity, not a library. For example, a regulator can't grant a registration that is still `InProgress`; the method returns a 409 instead. |
| **Domain event** | A small record such as `RegistrationDecisionRecorded`, raised by the entity when something meaningful happens. These events end up as the published events. |
| **Outbox** | A table holding events waiting to be published (described above). It's what makes publishing reliable. |
| **Command / query** | A command changes data (POST/PUT). A query only reads (GET). Each has one handler class in its own folder. |
| **Result / Error** | The return type for expected failures (not found, not allowed, validation). The endpoint turns these into `ProblemDetails` with a stable `code`. Exceptions are only for real faults and become a 500. |
| **Module** | A self-contained feature area (Registration, Packaging) with its own tables, code and endpoints. |

### Why status is checked in one place

The workflow statuses are the core business rule. Registration goes like this:

```
InProgress ──submit──▶ SubmittedForApproval ──Grant──▶ Granted ──Cancel──▶ Cancelled
                              │  ▲          ──Refuse─▶ Refused
                         Query│  │submit
                              ▼  │
                            Queried   (a regulator can also Grant/Refuse/Query from here)
```

Today these statuses are recalculated on every read, in five places that disagree. Here the status is
**stored**, and changed in only one place: the entity's methods. The events you want to publish are exactly
these status changes, so the same method that changes the status also raises the event.

Updates that don't change a status are simpler. A handler loads the entity, sets the values and saves. Not
every endpoint needs an entity method or an event.

### Where to put new code

| You want to… | Add |
|---|---|
| Show new data on a screen | A query + handler in `Application/Queries/<UseCase>/`, and a `MapGet` in `Endpoints/` |
| Accept a create or update | A command + handler in `Application/Commands/<UseCase>/`, a public `*Request` type, and a `MapPost`/`MapPut` |
| Add a workflow step or status change | A method on the entity in `Domain/` that checks the current status, changes it, adds a history row and calls `Raise(...)`. Plus a domain test |
| Publish a new event | A record implementing `IDomainEvent` in `Domain/`, raised from the entity method. The outbox does the rest |
| Change the database | A new Liquibase changeset in `changelog/changes/<module>/`. Never edit an applied one |
| Add a fee rate, or a submission period | A new changeset in `changelog/changes/fees/` or `changelog/changes/submission-periods/`. No code change |
| Charge a fee in a workflow | Call `IFeesApi` from the command, and save the returned lines as the workflow's fee snapshot. See [Fees in a workflow](#fees-in-a-workflow-snapshots-and-line-items) |
| Answer a question that combines packaging and registration | A query in the module that owns the question, joining the other workflow's tables read-only. See [Questions that combine domains](#questions-that-combine-domains) |

## Layout

```
WasteSubmitRegPackagingApi/                      Host: Program.cs, CDP plumbing (auth, logging, Postgres, trust store).
                                                 Wires up modules; holds no business logic.
WasteSubmitRegPackaging.SharedKernel/            Result/Error, ICommand/IQuery + handlers, AggregateRoot, IDomainEvent, ProducerUser/RegulatorUser.
                                                 No dependencies at all.
WasteSubmitRegPackaging.SharedKernel.Infrastructure/
                                                 ModuleDbContext, outbox (interceptor + dispatcher), Result → ProblemDetails,
                                                 user headers, handler registration.
WasteSubmitRegPackaging.Registration.Contracts/  What Registration offers other modules (IRegistrationModuleApi + DTOs).
WasteSubmitRegPackaging.Registration/            The Registration module.
WasteSubmitRegPackaging.Packaging/               The Packaging module.
WasteSubmitRegPackaging.Fees.Contracts/          What Fees offers other modules (IFeesApi + DTOs).
WasteSubmitRegPackaging.Fees/                    The Fees module: fee tables and calculations (fee_* tables).
WasteSubmitRegPackaging.SubmissionPeriods.Contracts/
                                                 What SubmissionPeriods offers other modules (ISubmissionPeriodsApi + DTOs).
WasteSubmitRegPackaging.SubmissionPeriods/       The SubmissionPeriods module (period_* tables).
changelog/                                       Liquibase: db.changelog.xml → changes/<module>/NNNN-*.xml
WasteSubmitRegPackagingApi.Test/                 Tests, foldered by type: Architecture, Unit, Http, Database, Integration, Contract.
```

Inside each module, folders are layers. Each one answers a single question:

```
Domain/          What are the rules?      Aggregates, state machines, domain events, errors. No EF Core,
                                          ASP.NET Core or Npgsql. Left out when a module has no rules (SubmissionPeriods).
Application/     What can be done?        One folder per use case: Commands/<UseCase>/ or Queries/<UseCase>/,
                                          holding the command or query, its handler and its view.
Infrastructure/  How is it stored?        The DbContext, EF mappings, repositories, and the Contracts implementation.
Endpoints/       How is it called?        Minimal API routes and public request types. Thin: bind, call the handler, map.
<Name>Module.cs  How is it wired up?      The only public entry point: Add<Name>Module() and Map<Name>Endpoints().
```

To find code, start from the use case folder: everything one endpoint does is in `Application/<Commands|Queries>/<UseCase>/`.

## Dependency rules (enforced by `ArchitectureTest`)

- Modules never reference each other. A module may reference another's `*.Contracts` project (for example
  Packaging will use `Fees.Contracts` to charge resubmission fees), never the module itself. The project
  references make this a compile error, not just a review comment.
- Fees and SubmissionPeriods depend on no other module, because the workflows depend on them.
- `Domain` doesn't depend on EF Core, ASP.NET Core, Npgsql or any other layer of its module.
- `SharedKernel` has no infrastructure dependencies.
- Only `<Name>Module`, `*Request` types and the enums they use are public. Everything else is `internal`.
- Each module owns its tables (`reg_*`, `pkg_*`, `fee_*`, `period_*`), and **only that module writes to them**.
- **The fee and period tables are shared.** Any module may join to the `fee_`* and `period_*` tables and hold
  foreign keys to them (for example a packaging submission → a `period_packaging_data_period` code). Only their Liquibase changesets
  change them.
- **There are no foreign keys between the workflow modules.** Store the other module's ID or key (for example
  `pkg_packaging_submission.registration_year`). Reads may join across them, but writes never cross. See
  [Questions that combine domains](#questions-that-combine-domains).

## Anatomy of a use case

Take *"a regulator grants a registration"* as the example: `POST /regulator/registrations/{id}/decisions`.

1. **Endpoint** (`Endpoints/RegistrationEndpoints.cs`). Binds the request, builds the command with the regulator user,
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
| Endpoint filter | Who: `.RequireProducer()` / `.RequireRegulator()` | 400 `user.missing`, 403 `user.producers_only` / `user.regulators_only` |
| Handler | Application rules: own organisation, required-when rules, anything that needs the database | 400, 403 or 404 `ProblemDetails` |
| Aggregate | Invariants: is this transition allowed now? | 409 `ProblemDetails` |

Expected failures are returned as `Result`/`Error` values, never thrown. Every error has a stable `code`
(for example `registration.decision_not_allowed`) in the ProblemDetails, so the frontends can switch on it.
Exceptions are kept for bugs and infrastructure faults, and become a 500.

> **Gotcha found while building this:** the .NET 10 validation source generator silently **skips internal
> request types**. The request then reaches the handler unvalidated. That's why request types are public;
> `ArchitectureTest` checks it.

## Commands, queries and state

- Commands go through the aggregate and a repository. Queries read the module's DbContext, either in LINQ
  with `AsNoTracking()` (see `GetPackagingSubmission`) or in SQL with `Database.SqlQuery` (see
  `GetPackagingSubmissionPeriods`), and map into view models. See [Data access](#data-access-linq-or-sql-both-in-ef-core).
- **State is stored, not recalculated.** The aggregate keeps its current `Status`, and history is an append-only
  table. That's the main change from today's Cosmos/Synapse model, where every read replays events, and that
  logic exists in five places that disagree.
- **The backend returns ready-to-render state.** For example, `AvailableRegulatorDecisions` tells the
  regulator UI which buttons to show, so the frontends don't re-derive rules.

## DTOs and mapping

**Entities never leave their module, and never go over HTTP.** Every boundary has its own plain C# record or
class, and the mapping between them is **written by hand**. There's no AutoMapper, Mapster or Mapperly.

| DTO | Lives in | Example | Who sees it |
|---|---|---|---|
| **Request** | `Endpoints/*Requests.cs` (public) | `StartRegistrationRequest` | Callers, as JSON. Shape checks only (`[Required]`, `[StringLength]`, `[Range]`) |
| **Response** (writes) | `Endpoints/` | `StartRegistrationResponse(Guid RegistrationSubmissionId)` | Callers, as JSON. Usually just the new id, or nothing (`204`) |
| **Command / query** | `Application/<Commands\|Queries>/<UseCase>/` | `StartRegistrationCommand` | The handler. Carries typed users (`ProducerUser`) and domain types such as `RegistrationSubmissionId` |
| **View** (reads) | Next to its query | `RegistrationSubmissionView`, `RegistrationHistoryItem` | Callers, as JSON. Ready to display, including worked-out values like `AvailableRegulatorDecisions` |
| **Contract** | `<Module>.Contracts` | `RegistrationSummary`, `RegistrationState` | Other modules only |

### The flow, and where each mapping happens

```
HTTP JSON ──▶ Request ──(endpoint)──▶ Command ──(handler)──▶ Aggregate ──▶ DB
                                                                          │
HTTP JSON ◀── View ◀──(query handler, AsNoTracking)◀─────────────────────┘
Other module ◀── Contract DTO ◀──(Infrastructure/<Module>ModuleApi)◀──────┘
```

1. **Request → command, in the endpoint method.** Field by field, adding the user from `ICurrentUser`.
   `[Required]` has already run, so `request.OrganisationId!.Value` is safe. See `RegistrationEndpoints.StartRegistration`.
2. **Entity → view, in the query handler.** Load with `AsNoTracking()` and call `new XxxView(...)`, mapping child
   collections with LINQ `Select`. See `GetRegistrationSubmissionHandler`.
   - Load the aggregate when the view needs a domain rule, as this one does for `AvailableRegulatorDecisions()`.
   - For lists and searches (such as the paged regulator list), **select straight into the view in the query**
     (`.Select(s => new XxxRow(...))`), so EF fetches only the columns you need. Don't load whole aggregates.
3. **Entity → contract, in `Infrastructure/<Module>ModuleApi.cs`.** Select only the columns you need, then map
   domain enums to the contract's own enums with an exhaustive `switch` that throws on an unknown value. Adding a
   domain status then fails loudly instead of leaking through. See `RegistrationModuleApi.ToContract`.
4. **Result → HTTP, in the endpoint.** Success is `TypedResults.Ok(view)`, `Created(...)` or `NoContent()`. A
   failure is `result.Error.ToProblem()`, which returns ProblemDetails with a stable `code`.

### Rules
- **One DTO per use case, kept in that use case's folder.** Don't share a "RegistrationDto" across endpoints. Two
  screens that look alike today will drift apart.
- **Map by hand, next to the code that uses it.** Renames are compile errors, every mapping can be found with
  "Find usages", and there's no hidden setup. A few lines per use case is cheaper than a mapping library.
- **No mapping in the domain.** Entities don't know about DTOs. The endpoint or handler does the mapping.
- **Requests are `public` classes with nullable properties.** A missing field is then a 400, not a silent
  default. They must be public because the validation source generator skips internal types (see above).
  Everything else is `internal` records.
- **Requests carry only what the caller chooses.** The acting user, ids from the route and the time never come
  from the body. They come from `ICurrentUser`, the route and `TimeProvider`.
- **Contracts never expose domain types.** They use only primitives and the contract's own enums.

### Known compromise: domain enums appear in the API
Requests and views use the domain's `Nation`, `RegistrationJourney`, `RegistrationStatus` and `RegulatorDecision`
directly, serialised as strings through `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` on the domain enum.
This keeps the skeleton small, but it has two costs:
- **Renaming a domain enum value silently changes the API**, and breaks the frontends and any service that calls it.
- **The domain carries a JSON attribute.** It's only `System.Text.Json`, so the architecture rule still holds,
  but it's a serialisation concern in the domain.

Only the cross-module contract has its own enums. If the API needs to be more stable, give requests and views
their own enums, mapped like `ToContract`, or at least add a test that pins the JSON values. See discussion
point 8.

### Adding a use case: the DTO checklist
- [ ] A `public sealed class XxxRequest` (writes) with DataAnnotations, in `Endpoints/`.
- [ ] An `internal sealed record XxxCommand` or `XxxQuery`, in its use case folder.
- [ ] An `internal sealed record XxxView` or `XxxResponse`, next to the query or in `Endpoints/`.
- [ ] Mapping in the endpoint (request → command) and in the handler (entity → view).
- [ ] An endpoint test that checks the JSON shape, so the contract with callers (frontends and services) is pinned.

## Persistence

- **Liquibase owns the schema.** The service runs as CDP's DML-only user, so EF never migrates. Locally,
  `compose.yml` mirrors this: `compose/postgres/init.sql` creates a DDL user and a DML-only app user, and a
  `liquibase` service applies `changelog/` before the app starts.
- **Changesets are never edited** once applied, because Liquibase checksums them. Ids are
  `<module>-<NNNN>-<what>`, so the teams can't clash.
- **Naming.** EF uses the snake_case convention (`ApplicationReferenceNumber` → `application_reference_number`).
  Table names are set explicitly with the module prefix. `RegistrationDatabaseTest` applies the real changelog to
  Postgres and round-trips an aggregate, which proves the mapping and changelog agree.
- **IDs** are strongly typed (`RegistrationSubmissionId`) and created in code with `Guid.CreateVersion7()`.
- **Time** is `DateTimeOffset` UTC from an injected `TimeProvider`, so tests use a fixed clock.
- **Enums** are stored as text with CHECK constraints, not PG enum types. The exception is a value that has
  an id in the source system, which is stored as that id: `Nation` is stored as `nation_id` (1 GB-ENG,
  2 GB-SCT, 3 GB-WLS, 4 GB-NIR, the ids `fee_regulator` uses too), so data carried over from Azure maps straight
  across. The API uses the codes (`GB-ENG`) and also accepts the ids.
- **Fee and period ids** are the source system's ids (integers set in the changeset), not generated, for the
  same reason. Fee dates are `date` (inclusive), and money is `numeric(19,4)` in pence, as in the payment service.
- **Concurrency.** Postgres `xmin` is used as a row version, so two regulators deciding at once gives one success and
  one 409 (`registration.concurrent_change`).
- **Unique clashes** are recognised from the Postgres error code (`23505`), never by matching message text.

## Data access: LINQ or SQL, both in EF Core

**EF Core is the only data access library.** A query is written in LINQ or in SQL, whichever reads more clearly.
Writes always go through the domain classes and `SaveChanges`.

| | LINQ | SQL (`Database.SqlQuery<TRow>`) |
|---|---|---|
| **Use it for** | Commands, and queries over the module's own tables | Queries that are clearer as SQL: joins across tables, reports, questions that combine domains, anything an analyst should be able to read |
| **Reads into** | Domain classes (when a rule is needed) or a view, with `.Select(...)` | A row class shaped like the SELECT, then mapped to the view |
| **Example** | `StartRegistration`, `GetPackagingSubmission` | `GetPackagingSubmissionPeriods` |

**Why not Dapper as well?** Some teams use EF Core for writes and Dapper for reads (Microsoft's 2017
eShopOnContainers did). That was mostly because older EF Core couldn't run SQL into a plain class. EF Core 8 added
`SqlQuery<T>`, so one library now does both, with one connection setup, one naming convention and one way to test.

**Rules for a SQL query**
- **Read only.** No `INSERT`, `UPDATE` or `DELETE` in SQL. Saving goes through the domain classes and
  `SaveChanges`, which is what writes the change, its event and its history together.
- **Values go in `{braces}`**, as in `$"... WHERE p.code = {query.PeriodCode}"`. EF sends them as parameters, so
  this is safe. Never build the SQL with `+` or `string.Format`.
- **Columns are snake_case** and map to the row's properties by the same convention as the tables
  (`start_month` → `StartMonth`). Use `AS` only to rename (`r.id AS registration_period_id`).
- **Read into a private row class, then map to the view or contract** in the handler. Rows are shaped like the
  SELECT; views are shaped like the screen or caller. Never read SQL into a domain class.
- **Child rows**: one query per level, joined in C# with `ToLookup`. Avoid one big join that repeats the parent on
  every row.
- **Types**: `timestamptz` reads as `DateTimeOffset`, `timestamp` as `DateTime`, and an array works with
  `= ANY({ids})`.
- **Test it on Postgres**, as `SubmissionPeriodsDatabaseTest` does. That's what proves the SQL matches the
  changelog; the compiler can't.

## Outbox

Domain events are written to the module's outbox table (`reg_outbox_message`, `pkg_outbox_message`) by
`DomainEventsToOutboxInterceptor`, in the same transaction as the change. `OutboxDispatcher<TContext>`
polls with `FOR UPDATE SKIP LOCKED`, so several instances are safe, and passes each message to an
`IOutboxMessagePublisher`. The skeleton's only publisher logs. Real ones would be GOV.UK Notify, an
SNS/SQS feed, or the downstream registration feed. The dispatcher is off unless `Outbox:DispatcherEnabled` is
true. Delivery is at-least-once, so publishers must cope with duplicates.

## Calling another module

A module offers other modules a contract: an interface in its `*.Contracts` project, implemented inside the module.
For example, Registration offers `IRegistrationModuleApi` (with its own `RegistrationState` enum, separate from the
domain's, so Registration can change its internals freely), and Fees offers `IFeesApi`. If a module is ever split
into its own service, the contract becomes an HTTP client and callers don't change.

Use a contract when **one module needs an answer from another in order to make a decision**, inside a command (for
example, Packaging asking Fees for the resubmission fee when a resubmission is submitted). Don't add a check that
the business doesn't have: the sample packaging command doesn't require a registration, because producers report
packaging data before they register. For questions a
*caller* asks across both domains, see [Questions that combine domains](#questions-that-combine-domains).

## Shared modules: Fees and SubmissionPeriods

Fees and submission periods are shared lookup data: the same for every caller, changed rarely and only on
purpose, and used by every workflow. Each has its own module, tables, changelog folder and contract.

```
Fees (fee_*)                                                  used by
  fee_regulator        1 GB-ENG ... 4 GB-NIR                  Packaging: resubmission fee
  fee_group            5 ProducerResubmission, 6 ...          Registration (later): registration,
  fee_sub_group        7 ReSubmitting, ...                      late and subsidiary fees
  fee_rate             amount (pence) per group, sub-group,
                       regulator and date period

SubmissionPeriods (period_*):
  period_registration_submission_period   one per window type and registration year (CsoLargeProducer 2026 ...):
                                          the payment service's [Lookup].[SubmissionPeriod] rows and ids
  period_packaging_submission_period      one per submission period ("July to December 2025"): visible from,
                                          opening and deadline dates
  period_packaging_data_period            one per data period code (2025-H2, 2025-P0 ...): what the data covers,
                                          who reports it (Large: H1, H2; Small: P0), and the submission period
                                          that accepts it
```

- **When and what.** Both domains have a *when* (when you can submit) and a *what* (registration covers a
  registration year; packaging covers a data period such as 2026-H1). Registration keeps the payment service's single
  table. Packaging has a table for each, because one submission period accepts several data periods and its dates
  are the same for all of them. Packaging links to registration by `registration_year` (the data year + 1).
- **Naming: a table is `period_` plus the domain name, and the C# names are the same without the prefix.**
  `period_packaging_submission_period` is read by SQL into `PackagingSubmissionPeriodRow` (one row class per table,
  in `Application/Queries/SubmissionPeriodRows.cs`), and grouped into the contract `PackagingSubmissionPeriod`.
- **They use the same ids as epr-payment-service.**
  - `fee_group`, `fee_sub_group`, `fee_regulator` and `fee_rate` mirror its `[Lookup]` tables, ids included.
  - `period_registration_submission_period` mirrors `[Lookup].[SubmissionPeriod]`, ids and values included. A test pins
    all 12 rows, because migrated registration data points at these ids. See
    [decision 0002](decisions/0002-registration-data-from-payment-service.md).

  So the registration fees, payments and line items that move later reference them with no conversion, and the
  registration rates are just new rows.
- **The data is owned by changesets** in `changelog/changes/fees/` and `changelog/changes/submission-periods/`.
  - A new rate or period is a new changeset, which is reviewed, versioned and applied per environment.
  - The app never updates the data. Both modules' DbContexts derive from `ReadOnlyDbContext`, which throws on save.
- **The packaging periods have moved off config.** They're seeded from the packaging frontend's `SubmissionPeriods`
  and the POM validation function's period codes. Changing those two services to read periods from here is
  follow-on work.
- **The packaging → registration link** is `registration_year`, not a foreign key, because a registration year has
  several registration submission periods (one per window type). The seeded rule is the one the frontend tells
  users ("report your 2025 packaging data before registering for 2026"). **The business owner still has to confirm it.**
- **Rates can't overlap.** A test checks that no two rates for the same fee and regulator overlap. A database
  `EXCLUDE` constraint would need the `btree_gist` extension, which CDP doesn't enable by default.
- **Other modules use them in two ways.** In C#, through `IFeesApi` (Fees.Contracts) and `ISubmissionPeriodsApi`
  (SubmissionPeriods.Contracts). In SQL, by joining to the tables in a query.
- **The two don't depend on each other.** A fee is matched to a rate by date, not by period. If a fee ever needs
  a period, the workflow passes the period in.
- **No validation error codes.** Today's per-period codes (54, 55, 65 ... 89) were config for the POM validation
  function. The validation project in this service owns its error codes in code: it checks a file's period codes
  against the chosen period's data periods (and the producer's size), with one stable error code.
- **Over HTTP** there are, for frontends and services (`read` scope, no user headers):
  - `GET /submission-periods/registration?registrationYear=`: the registration submission periods, as the payment
    facade's `submission-periods` endpoint gives the frontend today.
  - `GET /submission-periods/packaging?periodCode=`: every packaging submission period, for services; with a code,
    just the submission period and dates for that code.
  - `GET /submission-periods/packaging/for-submitter-type?submitterType=DirectProducer|ComplianceScheme&producerSize=Large|Small`:
    the tiles for the packaging data page. Only periods visible now (UK time), newest year first, each with the data
    periods this submitter reports and `isOpen`. A compliance scheme gets every size; a direct producer gets only its
    size when `producerSize` is given (a small producer: only P0 tiles).
  - `GET /submission-periods/packaging/{submissionPeriod}/for-submitter-type?...` (for example
    `/packaging/January%20to%20June%202026/for-submitter-type`): one period, for the pages after a tile is chosen
    ("January to June 2026 (H1)" is the data period's dates plus its `shortCode`). 404 if this submitter can't see it.
  - `POST /fees/packaging-resubmission`.

  The period endpoints return dates and codes, not sentences, so the frontend writes the English and Welsh text.
  - **The fee endpoint's input is pencilled in.** For now it takes the fee inputs directly.
  - It will take a `packagingSubmissionId` (and, for registration fees, a `registrationSubmissionId`).
  - Fees will then read the inputs (nation, date, member count, previous payments) from that submission
    through the workflow module's contract.

## Fees in a workflow: snapshots and line items

Fees **works out** fees, and the workflow that charges them **stores** them. This follows the
registration improvement already made in epr-payment-service (`RegistrationFeeSnapshot` and
`RegistrationFeeLineItem`). Packaging follows the same pattern when its workflow is built:

```
Packaging command (for example "submit a resubmission")
  1. Load the submission (the aggregate).
  2. Call IFeesApi.CalculatePackagingResubmissionFeeAsync(input)
       --> Fees looks up the rate and runs the pure calculator
       <-- fee lines (rate id, fee type, unit price, quantity, amount)
  3. Call submission.Submit(..., feeLines): status change, history row, fee snapshot, domain event.
  4. SaveChanges in one transaction: the submission, its events, pkg_fee_snapshot + pkg_fee_line_item, the outbox.
```

- **Fees stores nothing per submission.** The calculator is pure, so the same inputs always give the same
  lines. That's how parity with epr-payment-service is tested (`ResubmissionFeeTest`, `FeesDatabaseTest`).
- **The workflow owns its snapshot.** That's `pkg_fee_snapshot` and `pkg_fee_line_item` (later `reg_...` tables with
  the same shape), saved with the status change.
  - A line copies the unit price and description, so a later rate change can't alter what was charged.
  - It keeps a foreign key to the rate and the fee type, which is allowed because the fee tables are shared.
  - There is one snapshot per submission (or per resubmission), and it is never recalculated.
- **The outstanding amount is worked out when it's needed**, as the snapshot total less the payments linked to
  the submission, so it can't go out of date. Until payments move to CDP, the caller passes previous payments in.
- **Events** follow the registration pattern: a stored status, an append-only `pkg_*_event` history table, and
  an outbox event. Nothing is recalculated by replaying events.

## Questions that combine domains

**This is the main reason packaging and registration share one Postgres database.** Callers, especially other
services, need answers that span both. For example, Manage Obligations needs "the registration details for the
year a packaging submission relates to".

- **Most of it is shared data.** A packaging submission's period links to its registration submission period,
  and a registration is for a registration year. So the "which year" part is a join to the `period_*` tables,
  which every module may make.
- **For the workflow data, a query may join the other workflow module's tables, read-only.** Put the query in the
  module that owns the question (for obligations data, Packaging), and write it as SQL (`Database.SqlQuery`),
  because a module's DbContext maps only its own tables. Follow the [SQL query rules](#data-access-linq-or-sql-both-in-ef-core).
- **Writes never cross.** If a change in one domain must cause a change in another, use a domain event through
  the outbox, or a contract call inside a command.
- **Keep the list short and agreed.** Note which of the other module's tables and columns a query reads, so the
  owning team knows what it can't rename freely. If that list grows, the next step is a view that the owning
  module publishes.
- **Performance** comes from indexes on the join keys (`organisation_id`, the period, `registration_submission_id`).

For example (illustrative, because the workflow tables are still samples):

```sql
SELECT p.id AS packaging_submission_id, pps.name AS packaging_submission_period,
       r.id AS registration_submission_id, r.registration_year, r.status
FROM pkg_packaging_submission p
JOIN period_packaging_data_period pdp ON pdp.code = p.period_code
JOIN period_packaging_submission_period pps ON pps.name = pdp.submission_period
LEFT JOIN reg_registration_submission r
       ON r.organisation_id = p.organisation_id AND r.registration_year = pps.registration_year
WHERE p.organisation_id = @organisationId;
```

**The cost:** these queries tie the two workflows to one database. That's acceptable, because answering combined
questions is the point of sharing it. If a module were ever split out, they would need a read model fed by events.

## API callers: frontends and services

The endpoints are a **public API**, not a private backend for one UI. Design them so a service can use them
as easily as a frontend:

- **Shape endpoints around the question, not the screen**, when a service is the caller. For example, return
  "packaging submission plus its registration" in one call, rather than making the caller join two responses.
- **Paging and filtering** on every list (`?page=&pageSize=`, plus filters such as year, status and
  organisation), with a stable sort order, so a service can page through everything reliably.
- **Stable JSON.** Add fields freely, but never rename or remove one, or change an enum value, without a
  new version. Pin the shape with an endpoint test (see [DTOs and mapping](#dtos-and-mapping)).
- **Versioning.** If a breaking change is unavoidable, add a new route (`/v2/...`) alongside the old one until
  every caller has moved.
- **Codes, not text.** Errors carry a stable `code`, and values are codes (`GB-ENG`, `Granted`), so callers
  (and the Welsh frontend) map them to text themselves.
- **No user headers for service calls.** The `X-User-`* headers identify a person acting in a frontend.
  Reads of shared data (fees, submission periods) don't need them. Only endpoints marked `.RequireProducer()` or
  `.RequireRegulator()` do: commands, and reads of one organisation's data, where the handler also checks the
  producer acts for that organisation.
- **Document it.** Each endpoint has a summary and description in OpenAPI, so other teams can use
  `/swagger` (or the published spec) without asking.

## Security and the acting user

- **Services** are authenticated by the existing ACL (Basic API key per client). Queries need the `Read` policy
  (scope `read`) and commands need `Write` (scope `write`, added on this branch). **Every caller gets its own
  client**: each frontend, and each service such as Manage Obligations, with only the scopes it needs. That gives
  each caller its own key to rotate or revoke, and its own entry in the logs. Most services only need `read`.
- **The user.** The domain names the people who use the service: a `ProducerUser` (user id and the organisation
  they act for) and a `RegulatorUser`. Domain methods ask for the one allowed to call them, for example
  `RecordDecision(..., RegulatorUser decidedBy, ...)`, so a producer can't make a decision: it doesn't compile.
  The user comes from the `X-User-Id`, `X-User-Type` and (producers) `X-Organisation-Id` headers, checked by
  `.RequireProducer()` or `.RequireRegulator()` on the endpoint. **The headers are a placeholder:** see the open
  questions.

## Tests

The test project is foldered by type of test, then by module (`Platform` for auth, health, metrics and timeouts).
The rules for where a test goes, and what belongs in the journey and performance test repos instead, are in
`CLAUDE.md`, "Testing".

| Folder | What it proves | Example | Needs |
|---|---|---|---|
| `Architecture/` | Module boundaries and endpoint conventions | `ArchitectureTest` | Nothing |
| `Unit/` | Rules in the domain, a calculator or a handler (most rule tests belong here) | `RegistrationSubmissionTest`, `ResubmissionFeeTest`, `RecordRegulatorDecisionHandlerTest` (NSubstitute) | Nothing |
| `Http/` | The HTTP layer: real host, with the handler or repository substituted | `RegistrationEndpointsTest` | Nothing |
| `Database/` | SQL, the EF mapping and the seeded data, on Postgres built from the Liquibase changelog | `RegistrationDatabaseTest`, `FeesDatabaseTest`, `SubmissionPeriodsDatabaseTest` | Docker; skipped without it, as in the Dockerfile build |
| `Integration/` | The real host and Postgres together | `PostgresHealthCheckTest` | Docker, as above |
| `Contract/` | The v1 OpenAPI snapshot | `OpenApiTest` | Nothing |
| `Support/` | Factories, fixtures and test data; no tests | `AclTestFactory`, `ChangelogDatabase` | |

## Where the patterns come from

| Piece | Source |
|---|---|
| Minimal APIs, built-in validation, ProblemDetails (RFC 9457), `TimeProvider`, EF Core | Microsoft's ASP.NET Core and EF Core documentation |
| Layers: Domain / Application / Infrastructure / Endpoints | Microsoft's *Architecting Modern Web Applications with ASP.NET Core and Azure* (the eShopOnWeb reference app): Clean Architecture |
| Aggregates, domain events, commands and queries kept apart, a repository per aggregate | Microsoft's *.NET Microservices: Architecture for Containerized .NET Applications* (the eShopOnContainers ordering service) |
| SQL queries into row types (`SqlQuery<T>`) | EF Core documentation, "SQL Queries" (querying unmapped types, EF Core 8+) |
| Transactional outbox | Azure Architecture Center, "Transactional Outbox pattern" |
| Shared lookup data that every domain reads | A standard DDD pattern ("reference data") |
| A fee snapshot with line items, priced at the time of charging | The invoice-line pattern, as in epr-payment-service's own registration fee snapshot |
| One folder per use case | Vertical slices, a common .NET community practice |
| Modules in one service, each with a Contracts project | The "modular monolith", a common community practice with no formal Microsoft guidance |
| `Result`/`Error` values, strongly typed ids | Community practice, and optional. Microsoft's samples mostly throw exceptions and map them to ProblemDetails |