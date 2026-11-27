# CLAUDE.md

This is a .NET 10 modular monolith: one API on DEFRA CDP (Aurora Postgres) for EPR packaging and registration.
New code must look like the code already here. Before you change anything, read `docs/project_architecture.md`,
especially "Where to put new code", "Anatomy of a use case" and "Adding a use case: the DTO checklist".

## How to work
- **Plan first.** Before writing code, list the files you will add or change, and say which existing slice you are
  copying.
- **Build only the requirement.** State it in one line first. No extra hardening, config settings or abstractions
  unless asked; suggest them as follow-ups instead.
- **Cross-cutting code: framework, then library, then pattern.** For auth, HTTP, caching, retries, validation or
  logging, check in this order: a built-in ASP.NET Core feature, a well-maintained library (already referenced, or
  widely used), then an existing pattern here or in Manage Obligations (MO). Name the options in two or three lines and
  recommend one with a reason. Copying MO is a choice, not the default. Keep MO-consistent only what other services
  rely on: cdp-app-config shapes, header names, client ids.
- **Stop if it grows.** If a simple requirement needs more than about 200 lines or 5 files, stop and say why before
  continuing.
- **Copy an existing slice for domain use cases.** Don't invent a new structure. Reference slices:
  - a command: `Registration/Application/Commands/RecordRegulatorDecision/`
  - a query: `Registration/Application/Queries/GetRegistrationSubmission/`
  - shared reference data with SQL: `SubmissionPeriods/Application/Queries/GetPackagingSubmissionPeriods/`
- **Keep it small.** Don't add infrastructure, libraries or abstractions the change doesn't need. Ask first.
- **Don't invent domain language.** Use existing EPR terms: DirectProducer, ComplianceScheme, submission period,
  data period. Search the code and docs for a term before you introduce one; if there isn't one, ask.

## A use case is a vertical slice inside one module
1. **Request:** a `public sealed class XxxRequest` with DataAnnotations, in `Endpoints/`. Shape errors give a 400.
2. **Endpoint:** a thin `MapGet`/`MapPost` in `Endpoints/XxxEndpoints.cs`. It has a `.WithName(...)`, a `.WithSummary(...)`
   and **`.Unpublished()`**, and `.RequireAuthorization(ModulePolicies.Read)` for a query or `ModulePolicies.Write` for
   a command. It maps the request to a command or query, calls the handler, and returns
   `result.IsSuccess ? TypedResults.X(...) : result.Error.ToProblem()`. Add `.RequireProducer()` or
   `.RequireRegulator()` when a user acts or reads one organisation's data.
3. **Command or query, and its handler:** one folder per use case under `Application/Commands/<UseCase>/` or
   `Application/Queries/<UseCase>/`, with `internal sealed` records and the handler together. Application rules go here
   (own organisation, required-when rules, anything that needs the database). Handlers are registered automatically.
4. **Domain:** state changes go through a method on the aggregate in `Domain/`. It checks the current status, changes
   it, adds a history row and calls `Raise(...)` with an `IDomainEvent`. A bad transition returns a Conflict (409).
5. **Errors:** return a `Result`/`Error` and never throw for expected failures. Errors live in the module's
   `XxxErrors` class with a stable code (`registration.not_found`), because callers switch on the codes.
6. **Persistence:** go through the module's repository or `DbContext`. Schema and reference data change through a new
   Liquibase changeset in `changelog/changes/<module>/`, with id `<module>-<NNNN>-<what>`, included from
   `changelog/db.changelog.xml`. Never edit a changeset that has been applied. Columns are snake_case.
7. **Tests:** see "Testing" below for where each one goes. A use case normally needs:
   - a `UnitTests/` test for the rules (domain or handler);
   - a `ServiceTests/` `XxxEndpointsTest` that calls the endpoint and pins the JSON shape and the failures;
   - a `ServiceTests/` `XxxDatabaseTest` when a rule lives in SQL or a changeset changes.

## The packaging workflow
The Packaging module holds a thin sample of the whole packaging data workflow: one submission, its files and its
cycles, one status, and one `NextStep()`. `docs/packaging_workflow.md` says what is in place and why. It is a sample,
not a contract: it is there to be built on, and changed when it doesn't fit.

- **Read `docs/packaging_workflow.md` before planning any change to the Packaging module.**
- **Say what is already there.** In the plan, first tell the developer what is in place for the step they are working
  on, then how the change fits it: which aggregate method, command, table and next step it touches.
- **Don't assume, and don't work round the shape.** If the change doesn't fit, say so plainly and put the options to
  the developer: extend the shape, or change it. Never quietly add a flag, a status or a table to get past it.
- **The frontend routes on `nextStep`.** Don't add state the frontend has to interpret. A new step for the producer
  is a new `PackagingNextStep` value; anything else is a fact recorded on the submission, a file or a cycle.
- **Keep the page true.** A change to the workflow updates `docs/packaging_workflow.md` in the same change.

## Module boundaries (enforced by `ArchitectureTest`)
- Modules:
  - `Registration` (`reg_*`) and `Packaging` (`pkg_*`) are workflow modules;
  - `Fees` (`fee_*`) and `SubmissionPeriods` (`period_*`) are shared reference data;
  - each module's `*.Contracts` project is its only public face.
- A module never references another module, only its `*.Contracts` project. Registration and Packaging don't reference
  each other.
- Domain code doesn't depend on EF Core or ASP.NET. Only request types, the module's entry point and the enums those
  use are public; everything else is `internal`.
- The host (`WasteSubmitRegPackagingApi`) only wires modules up, through `AddXxxModule()` and `MapXxxEndpoints()`. Don't put
  module logic in it.

## The API contract
- New endpoints start `.Unpublished()`. They're left out of the v1 OpenAPI document, and return 404 in deployed
  environments unless `UnpublishedEndpoints:Enabled` is set.
- The v1 contract is a committed snapshot: `OpenApiTest.The_v1_document_matches_the_approved_contract.verified.json`.
  Never accept a change to it without saying so. Publishing an endpoint (removing `.Unpublished()`) is a decision
  for the team, not part of a code change: see "Publishing an endpoint" in the README.
- Stable JSON: add fields freely. Don't rename or remove fields, or change enum values, in published endpoints.

## Testing
Tests are in `WasteSubmitRegPackagingApi.Test/`. Put a test in the folder for its type, then the module (`Platform`
for auth, health, metrics, timeouts). Pick the lowest type that can prove the behaviour.

| Folder | What it proves | May use |
|---|---|---|
| `ArchitectureTests/` | Module boundaries, and conventions every endpoint must meet | Reflection only |
| `UnitTests/` | Business rules in an aggregate, calculator, mapper or handler | Nothing; NSubstitute for a repository |
| `ServiceTests/` | The service running, on the real handlers and the seeded data | The host in memory and Postgres from `ChangelogDatabase` |
| `ContractTests/` | The v1 OpenAPI document is unchanged | The snapshot |

A service test is one of two kinds, and its class name says which:

| Class | What it proves | How |
|---|---|---|
| `XxxEndpointsTest` | An endpoint works: binding, validation, status codes, error codes, JSON shape, and the answer from the seeded data | Over HTTP, through the real host, handlers and Postgres |
| `XxxDatabaseTest` | SQL, EF mapping against the changelog, seeded reference data | A handler or the `DbContext` called directly on Postgres |

- A rule goes in `UnitTests/`. If the rule lives in SQL it goes in an `XxxDatabaseTest`. Never test a rule only over
  HTTP.
- Nothing is substituted in `ServiceTests/`: no NSubstitute handler, no in-memory repository. Use the data the
  changelog seeds; a test that needs a row of its own saves it through the real `DbContext`.
- Two things may be replaced: the clock (`FixedTimeProvider`), and a service this one calls (epr-common-data-api,
  with `StubApprovedSubmissionsProvider` or a fake HTTP handler).
- Service tests on a module's data need Docker.
- One test for success and one for each failure the requirement names. Don't test the framework, a library, default
  settings or the unpublished OpenAPI document.
- Don't write a "no credentials is a 401" test for a new endpoint: `EndpointConventionsTest` fails if any endpoint
  lacks the read or write policy. Don't repeat in `ContractTests/` what the v1 snapshot already pins.
- Tests that need Docker skip without it locally and in the image build, and fail in CI (`CI=true`).
- Shared factories, fixtures and test data go in `Support/` (no tests there). Reuse `AclTestFactory`,
  `ChangelogDatabase` and `TestUsers` before writing a new one.
- A unit test class is `XxxTest`. Keep to the two service test names above.
- Start the host with `AclTestFactory(PostgresSettings.For(database.ConnectionDetails))`.

Not in this repo. Both suites cover published endpoints only, and publishing an endpoint means adding it to both:
- **waste-submit-journey-tests:** does a deployed environment work? One 200 with the expected shape per endpoint, one
  401, and one unpublished endpoint that is a 404. No business rules, and no assertions on specific data.
- **waste-submit-performance-tests:** is an endpoint fast enough in perf-test? Status and shape checks only.

## Not decided yet: don't build on these without asking
- The trust model for the user headers (`X-User-Id`, `X-User-Type`, `X-Organisation-Id`) is a placeholder.
- Every endpoint except health needs a service-to-service API key client (the ACL from `main`, with `read` and
  `write` scopes). Don't remove or bypass it. The one exception is the CDP uploader's callback, which a secret token in its
  URL protects instead (`docs/packaging_workflow.md`); don't add another without asking.
- The outbox works, but its publisher only logs. Routing by event type and a real sender aren't built.
- The registration, regulator and packaging submission endpoints are SAMPLES (`.AsSample(...)`). They will change.

## Build and test
```bash
docker compose up -d postgres && docker compose up liquibase   # local database and schema
dotnet build
dotnet test                                                    # must pass, including ArchitectureTest and the OpenAPI snapshot
dotnet run --project WasteSubmitRegPackagingApi                # http://localhost:8085/swagger
```
Run `dotnet test` before you say a change is finished, and report any failures as they are.

## Keep docs light
Update `README.md` or `docs/project_architecture.md` only when a pattern or a rule changes, not for every feature.
Don't commit or push unless asked.
