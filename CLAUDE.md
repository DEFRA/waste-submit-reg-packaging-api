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
   - a `Unit/` test for the rules (domain or handler);
   - an `Http/` test that pins the JSON shape and the failures;
   - a `Database/` test when SQL or a changeset changes.

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
| `Architecture/` | Module boundaries, and conventions every endpoint must meet | Reflection only |
| `Unit/` | Business rules in an aggregate, calculator, mapper or handler | Nothing; NSubstitute for a repository |
| `Http/` | Binding, validation, status codes, error codes, auth, JSON shape | Host in memory, handler or repository substituted |
| `Database/` | SQL, EF mapping against the changelog, seeded reference data | Postgres from `ChangelogDatabase` |
| `Integration/` | A published endpoint works through the real host and Postgres together | Both, nothing substituted |
| `Contract/` | The v1 OpenAPI document is unchanged | The snapshot |

- A rule goes in `Unit/`. If the rule lives in SQL it goes in `Database/`. Never test a rule only over HTTP.
- `Http/` tests don't need Docker. If a test needs Postgres it is `Database/` or `Integration/`.
- `Integration/` is for published endpoints only: one success per endpoint. Failures belong in `Http/`.
- One test for success and one for each failure the requirement names. Don't test the framework, a library, default
  settings or the unpublished OpenAPI document.
- Shared factories, fixtures and test data go in `Support/` (no tests there). Reuse `AclTestFactory`,
  `ChangelogDatabase` and `TestUsers` before writing a new one.
- Name the class for its type: `XxxTest`, `XxxEndpointsTest`, `XxxDatabaseTest`, `XxxIntegrationTest`.

Not in this repo. Both suites cover published endpoints only, and publishing an endpoint means adding it to both:
- **waste-submit-journey-tests:** does a deployed environment work? One 200 with the expected shape per endpoint, one
  401, and one unpublished endpoint that is a 404. No business rules, and no assertions on specific data.
- **waste-submit-performance-tests:** is an endpoint fast enough in perf-test? Status and shape checks only.

## Not decided yet: don't build on these without asking
- The trust model for the user headers (`X-User-Id`, `X-User-Type`, `X-Organisation-Id`) is a placeholder.
- Every endpoint except health needs a service-to-service API key client (the ACL from `main`, with `read` and
  `write` scopes). Don't remove or bypass it.
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
