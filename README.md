# waste-submit-reg-packaging-api

A .NET 10 API that serves aggregated packaging data from approved submissions to the Obligations service. It runs on
CDP in the protected zone, so it is reachable from other CDP services but not from the internet.

It calls [epr-common-data-api](https://github.com/DEFRA/epr-common-data-api)'s `v1/pom/approved-submissions/{periodYear}`
endpoint and groups its flat rows into this service's nested shape. The underlying stored procedure keeps only one
obligation row per producer for now (no tie-break between multiple real joiner/leaver events yet - see SUB-410's
`sp_GetApprovedSubmissionsMycWithObligations_ProducerIdFix`), so a producer currently shows at most one event, not its
full join/leave history; that will start returning more once the fix candidate is validated and cut over. The service
is connected to its own Postgres database.

* [Endpoints](#endpoints)
* [Running locally](#running-locally)
* [Configuration](#configuration)
* [Postgres](#postgres)
* [Health checks](#health-checks)
* [Swagger](#swagger)
* [Postman](#postman)
* [Testing](#testing)
* [CI/CD](#cicd)
* [Project layout](#project-layout)
* [Not used yet](#not-used-yet)
* [Licence](#licence)

## Endpoints

| Endpoint | Returns |
|---|---|
| `GET /packaging/{packagingYear}/producer-material-totals-by-submitter` | Every approved submission for the year, grouped by submitter, with each producer's material totals. Not paged. Requires the `read` scope |
| `GET /packaging/{packagingYear}/submitter-organisation/{submitterOrganisationId}/producer-material-totals` | The producers one submitter reported for: itself for a direct registrant, its members for a compliance scheme. Requires the `read` scope |
| `GET /health` | Whether the service is running |
| `GET /health/db` | Whether the service can reach Postgres |
| `GET /describe` | The service name. Requires authentication, see [Service-to-service authentication](#service-to-service-authentication) |
| `GET /swagger`, `GET /openapi/v1.json`, `GET /openapi/unpublished.json` | The API documentation, where Swagger is turned on. See [The OpenAPI contract for frontends](#the-openapi-contract-for-frontends) |

Both packaging endpoints return their results under `items`, which is empty when there is nothing for the year or for
that submitter. The submitter endpoint's `404` response is declared but not currently used: epr-common-data-api only
knows about organisations that submitted, so there is no way yet to tell "this organisation doesn't exist" apart from
"this organisation exists but has nothing approved this year" - both return `200` with empty `items`. Flagged as an
open gap in the SUB-410 plan. For a compliance scheme, `submitterOrganisationId` is the scheme's ID, not the
organisation ID of the company that runs it. Each producer carries its packaging in whole kilograms per material, and
its `events`: every time it joined or left during the year, oldest first.

```json
"events": [
  { "code": 1, "action": "Joined", "date": "2025-03-03" },
  { "code": 13, "action": "Left", "date": "2025-07-14" }
]
```

`code` is numbered as in the
[GOV.UK registration file guidance](https://www.gov.uk/guidance/register-for-epr-for-packaging-create-your-registration-file),
and `action` (`Joined` or `Left`) is worked out from it. `events` is `[]` when the producer did neither.

`StubApprovedSubmissionsProvider` still carries this example data for year `2024`, with two submitters, and the
existing endpoint tests still run against it (see [Testing](#testing)), but it is no longer what the running service
serves - see [Running locally](#running-locally).

| Submitter | `submitterOrganisationId` |
|---|---|
| A direct registrant | `11111111-1111-4111-8111-111111111111` |
| A compliance scheme with four members: one with no changes, one joining, one leaving, and one joining, leaving and rejoining | `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` |

## Running locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), Docker, and
[epr-common-data-api](https://github.com/DEFRA/epr-common-data-api) running locally (its own README covers that) -
`appsettings.Development.json` points `CommonDataApi:BaseUrl` at `https://localhost:5001`. Without it running, the
packaging endpoints return a `500`.

1. Start Postgres:

   ```bash
   docker compose up -d postgres
   ```

2. Run the service:

   ```bash
   dotnet run --project WasteSubmitRegPackagingApi
   ```

3. Check it is working:

   - http://localhost:8085/health should say `Healthy`
   - http://localhost:8085/health/db should say `Healthy` (it says `Unhealthy` if Postgres is not running)
   - http://localhost:8085/swagger shows the endpoints
   - http://localhost:8085/packaging/2024/producer-material-totals-by-submitter returns real data from epr-common-data-api

`dotnet run` uses the launch profile in `Properties/launchSettings.json`, which listens on port 8085 and loads
`appsettings.Development.json`.

### Running everything in Docker

```bash
docker compose up --build -d
```

This builds the service's image and starts it on port 8085 with everything in [compose.yml](compose.yml). The image runs
the tests while it builds. It runs with production settings, so Swagger is off; use `dotnet run` when you want Swagger.
Stop anything else on port 8085 first.

## Configuration

Settings come from three places, each overriding the one before:

| Where | Used for |
|---|---|
| `appsettings.json` | Defaults for CDP, such as the database name and user |
| `appsettings.Development.json` | Local overrides, such as a local Postgres with no password and Swagger on |
| [cdp-app-config](https://github.com/DEFRA/cdp-app-config) `services/waste-submit-reg-packaging-api/<env>/` | Per-environment values in CDP, as environment variables |

Environment variables use `__` for nesting, so `Postgres__Host` sets `Postgres:Host`.

| Setting | What it does | CDP value |
|---|---|---|
| `Postgres__Host` | The database cluster's writer endpoint. **Required**: the service will not start without it | Set per environment in cdp-app-config |
| `Postgres__Port`, `Postgres__Database`, `Postgres__Username` | Where to connect | `5432` and `waste_submit_reg_packaging_api`, from `appsettings.json` |
| `Postgres__UseIamAuthentication` | Sign in with an IAM token instead of a password | `true` |
| `Postgres__Password` | Local databases only. Ignored when IAM is on | Not set |
| `Postgres__SslMode` | TLS mode | `VerifyFull` |
| `Postgres__ConnectTimeoutSeconds`, `Postgres__CommandTimeoutSeconds` | How long to wait to open a connection, and how long one query may run | `15` and `30` (Npgsql's defaults), from `appsettings.json` |
| `Swagger__Enabled` | Serve Swagger and the OpenAPI document | `true` in dev and test only |
| `CommonDataApi__BaseUrl` | epr-common-data-api's base address. **Required**: the service will not start without it | Set per environment in cdp-app-config, once the cross-platform networking (Squid ACL, auth) is in place |
| `CommonDataApi__TotalRequestTimeout__Timeout`, `CommonDataApi__AttemptTimeout__Timeout`, `CommonDataApi__Retry__*` | Resilience for calls to epr-common-data-api, the same pipeline Manage Obligations uses: an overall timeout, retries, and a timeout per attempt | `00:01:00` overall, `00:00:20` per attempt, 2 retries with exponential backoff, from `appsettings.json` |

A change in cdp-app-config does nothing until the service is redeployed from the CDP Portal.

### Timeouts

Every call out of the service has a timeout, as the CDP production readiness review (PRR) requires. When one runs out,
the API answers `504 Gateway Timeout` with a problem response rather than holding the caller's request open, and the
request log records the 504. Other unhandled errors stay `500`. `CommonDataApiTimeoutTest` and `PostgresTimeoutTest`
prove the timeouts fire.

The resilience pipeline's timeouts cover the wait for epr-common-data-api's response headers. `HttpClient.Timeout` is
set to the same overall timeout, so a response whose body never finishes is cut off too. Manage Obligations leaves it
infinite, which leaves that case without a timeout.

## Postgres

The service has an Aurora PostgreSQL cluster (PostgreSQL 16) in every CDP environment, and connects to it with Npgsql.
CDP's guide is [Relational Databases on CDP](https://portal.cdp-int.defra.cloud/documentation/how-to/relational-databases.md).

### Postgres locally

`docker compose up -d liquibase` starts PostgreSQL 16 on port 5432 with the same database and user names as CDP, then
applies `changelog/` as the DDL user. It needs no password. `appsettings.Development.json` points the service at it, as
the DML-only user. `compose/postgres/init.sql` creates both users, but only on an empty volume: run
`docker compose down -v` once if you started Postgres before this change.

### Postgres in CDP

- **No password.** The service signs in with a 15-minute token from its IAM role, and fetches a new one every 10 minutes.
- **Two users.** The service uses `waste_submit_reg_packaging_api`, which can read and write data. Schema changes use
  `waste_submit_reg_packaging_api_ddl`. Tables are created by the Liquibase changelog in `changelog/` (see
  [Schema changes in CDP](#schema-changes-in-cdp)).
- **TLS.** `VerifyFull` checks the database's certificate against the RDS root certificates CDP supplies in
  `TRUSTSTORE_RDS_ROOT_CA`. If that ever fails, `Postgres__SslMode=Require` keeps the connection encrypted without
  checking the certificate.
- **Looking at the data.** Open a Terminal for the service in the CDP Portal and run `psql`, or
  `psql -U waste_submit_reg_packaging_api_ddl` for schema changes.

### Schema changes in CDP

The service never changes its own schema. A changeset reaches a CDP database in two steps:

1. **Publish.** `publish.yml` publishes `changelog/` on every push to main, with the same version as the service
   build, but only when the changelog has changed. Published versions can't be overwritten. To publish without a
   build (or the first time), run **Publish DB Schema** (`publish-db-schema.yml`) from the GitHub Actions tab.
2. **Apply.** In the CDP Portal, open [Apply Changelog](https://portal.cdp-int.defra.cloud/apply-changelog) (or **Apply**
   under *Database Changelogs* on the service's page), then pick the changelog version and the environment. The page
   shows what each environment already has. Liquibase runs as the DDL user, and the run's logs are in OpenSearch
   (index `cdp-codebuild`).

Every pull request runs **Check Liquibase Schema** (`check-schema.yml`). On a throwaway Postgres 16 with CDP's two
users, it validates the changelog, applies main's changelog and then the branch's on top (so editing a changeset main
already has fails, with a checksum error), and checks the app user can't create tables.

**Fix forward.** CDP doesn't roll back changesets. To undo or correct a change, add a new changeset; never edit or
delete one that has been merged. Changesets don't need a `<rollback>`. CDP sets `$ENV`, so a changeset can be limited to
some environments with `context="dev"` or `context="!prod"`. Details are in CDP's
[relational databases guide](https://portal.cdp-int.defra.cloud/documentation/how-to/relational-databases.md).

## Health checks

| Endpoint | Checks | Used by |
|---|---|---|
| `/health` | The service is running | CDP, to decide whether the container is alive |
| `/health/db` | A `SELECT 1` against Postgres | Us, to prove the database connection works |

`/health` leaves the database out on purpose. CDP restarts the container whenever `/health` fails, and a database blip
should not do that.

Neither health check ever requires authentication, whatever `Authorization` header is sent.

## Metrics

Every request is recorded as three CloudWatch custom metrics. The custom Grafana dashboard, `waste-submit-reg-packaging-api (custom)`, charts them. The names and tags match Manage Obligations (`waste-obligations`), so its dashboard panels work here too.

| Metric | Type | Recorded |
|---|---|---|
| `RequestReceived` | count | once per request |
| `RequestDuration` | milliseconds | once per request |
| `RequestFaulted` | count | when a request throws, including exceptions the error handler turns into a problem response |

- **Tags:**
  - `Service`
  - `RequestPath`: the route pattern, or `unknown` for unmatched URLs
  - `HttpMethod`: one of GET, HEAD, POST, PUT, PATCH, DELETE and OPTIONS, or `OTHER`
  - `StatusCode`: the status the caller got, such as 500 for an error or 504 for a timeout (see [Timeouts](#timeouts)). A caller that gives up first is recorded as 499 and isn't a fault.
  - `ExceptionType`: on `RequestFaulted` only
- **How they reach CloudWatch:** the metrics are written as AWS Embedded Metric Format (EMF) log lines.
- **Settings:**
  - On CDP, the platform sets `AWS_EMF_NAMESPACE` to the service name.
  - `AWS_EMF_ENABLED=false` turns export off. The tests do this.
  - Locally, `AWS_EMF_ENVIRONMENT=Local` prints the EMF lines to stdout. Both the launch profile and `compose.yml` set it.
  - With export on but no namespace, the app won't start.

## Service-to-service authentication

Callers authenticate with an API key, following the pattern used by the Manage Obligations service
([DEFRA/waste-obligations](https://github.com/DEFRA/waste-obligations)). CDP lists shared-secret
Basic auth as a supported option in its
[service-to-service auth guide](https://github.com/DEFRA/cdp-documentation/blob/main/how-to/service-to-service-auth.md).

The key is sent as HTTP Basic credentials: `Authorization: Basic base64(clientId:secret)`. Each
client is an entry in the `Acl:Clients` access control list:

| Setting | Where it lives | Example |
|---|---|---|
| `Acl__Clients__{clientId}__Type` | CDP app config | `ApiKey` |
| `Acl__Clients__{clientId}__Scopes__0` | CDP app config | `read` |
| `Acl__Clients__{clientId}__Secret` | **CDP secrets** (portal) | never in config or source |

The packaging submit frontend (waste-submit-reg-packaging-fe) calls as client `WasteSubmitRegPackagingFrontend`. Its `Type` and `Scopes` are in the service's `defaults.env` in CDP app config, as they are the same in every environment.

Endpoints opt in with `.RequireAuthorization(PolicyNames.Read)`, as in Manage Obligations, so a
client's scopes decide what it can call. Every endpoint except the health checks and the OpenAPI documents requires
`read` or `write`, and `EndpointConventionsTest` fails the build if one is mapped without a policy.
Client ids are matched case-insensitively and secrets are compared in constant time. Every client is validated at
startup: it must be an `ApiKey` client with a secret and known scopes. `read` is the only scope for now; add others, such
as `write`, when an endpoint needs them. The outcomes are:

- no credentials, or wrong credentials: `401`, with a `WWW-Authenticate: Basic` challenge
- a known client without the scope: `403` (not reachable yet, as `read` is the only scope)

There is deliberately no fallback policy.

**`GET /describe`** is a no-op that returns `{ "service": "waste-submit-reg-packaging-api" }` to an authenticated
caller. Use it to prove a caller's credentials work end to end.

**The health checks never require authentication.** CDP polls them, and an auth failure would mark
the service unhealthy. They are mapped with `.AllowAnonymous()`, and `EndpointConventionsTest`
fails the build if that changes or a fallback policy is added.

Locally, `appsettings.Development.json` (for `dotnet run`) and `compose.yml` (for Docker Compose) define a `Developer` client:

```bash
curl -i http://localhost:8085/describe                                # 401
curl -i -u Developer:developer-pwd http://localhost:8085/describe     # 200
curl -i http://localhost:8085/health                                  # 200, no auth needed
```

## Swagger

Swagger shows every endpoint, its parameters and its response shapes, and lets you call it from the browser.

### Swagger locally

1. Run the service (see [Running locally](#running-locally)).
2. Open http://localhost:8085/swagger.
3. Pick an endpoint, choose **Try it out**, fill in the parameters and choose **Execute**. Use year `2024` and the
   organisation IDs under [Endpoints](#endpoints).

The page has two OpenAPI documents (pick one at the top right):

| Document | URL | What's in it |
|---|---|---|
| **v1** | http://localhost:8085/openapi/v1.json | The published endpoints: the contract. Frontends generate their clients from this one. |
| **unpublished** | http://localhost:8085/openapi/unpublished.json | Endpoints that aren't published yet, including the sample registration, regulator and packaging submission endpoints. They may change, so don't generate from it. |

### Publishing an endpoint

Publishing decides whether an endpoint is in the v1 document, the contract that's merged and that frontends build
against, and whether it can be called once deployed.

- **To keep an endpoint unpublished**, add `.Unpublished()` to it or to its route group:

  ```csharp
  group.MapGet("/packaging/{id}/summary", GetSummary)
      .WithName("GetPackagingSummary")
      .Unpublished();
  ```

  `.AsSample(...)` makes a whole group unpublished and also marks it "(sample)".
- **To publish it**, remove `.Unpublished()` (or `.AsSample(...)`). The endpoint then appears in the v1 contract, the
  snapshot test fails, and accepting the new snapshot makes the change visible in the pull request.

**Publishing an endpoint is a formal step, not just a code change.** Once an endpoint is in the v1 contract, a frontend
can build on it and production traffic can reach it. So before the pull request that publishes it is merged:

- **Performance:** the endpoint must be performance tested as part of the wider performance-testing process: a CDP
  Performance Test Suite run in the perf-test environment, with realistic data volumes.
- **The contract:** it must be agreed with the teams that will call it, because changing it later breaks their
  generated clients.

Unpublished endpoints can only be called where `UnpublishedEndpoints:Enabled` is true: locally
(`appsettings.Development.json` and `compose.yml`). Everywhere else they're a 404 and Swagger doesn't list the
unpublished document. To try them in a deployed environment, for example dev, set `UnpublishedEndpoints__Enabled=true`
for it in cdp-app-config.

### The OpenAPI contract for frontends

Every build writes the documents to `WasteSubmitRegPackagingApi/openapi.json` (v1) and `openapi_unpublished.json`. They
aren't committed. Instead:

- **The approved v1 contract is committed** as
  `WasteSubmitRegPackagingApi.Test/ContractTests/OpenApiTest.The_v1_document_matches_the_approved_contract.verified.json`.
  A test fails when the API changes it, so every change to the contract shows up in the pull request. To accept a
  change, replace the `.verified.json` with the `.received.json` the failing run writes.
- **Each pull request build** lints `openapi.json` with Vacuum and attaches it as the `openapi.json` artifact.
- **For whatever is deployed**, fetch `/openapi/v1.json` from dev or test (below).

To call the API, a frontend needs its own ACL client: `Type: ApiKey` and its `Scopes` (`read`, plus `write` for
commands) under `Acl:Clients:{clientId}` in cdp-app-config, and the secret in CDP secrets as
`Acl__Clients__{clientId}__Secret`. It sends `Authorization: Basic base64(clientId:secret)`. `Developer` is only the
local client. Endpoints that act for a user also take the `X-User-*` headers listed in the document.

### Swagger in CDP

Swagger is on wherever cdp-app-config sets `Swagger__Enabled=true` (dev and test), and off everywhere else, including prod.

From a laptop, a protected-zone service can only be reached through the
[developer API key route](https://portal.cdp-int.defra.cloud/documentation/how-to/developer-api-key.md). Every request
needs an `x-api-key` header, which a browser will not send, so use the OpenAPI document rather than the page. With the
AWS OpenVPN connected and a Developer API key from your CDP Portal profile:

```bash
curl -H "x-api-key: $CDP_API_KEY" \
  https://ephemeral-protected.api.dev.cdp-int.defra.cloud/waste-submit-reg-packaging-api/openapi/v1.json \
  -o waste-submit-reg-packaging-api.openapi.json
```

That file is the contract for whatever is deployed. Import it into Postman (**Import**, then the file) or any OpenAPI
tool, or send it to a team that will call the API.

## Postman

[postman/waste-submit-reg-packaging-api.postman_collection.json](postman/waste-submit-reg-packaging-api.postman_collection.json)
covers every endpoint: both health checks, both packaging endpoints (including the empty and 404 cases), the API
documentation, and (locally only) `GET /describe` with and without credentials. It has two folders with tests on every
request.

| Folder | Calls | Before you run it |
|---|---|---|
| **1. Local** | The service on your machine | Start Postgres and the service (see [Running locally](#running-locally)) |
| **2. CDP dev** | The service deployed in CDP dev | Connect the AWS OpenVPN, and put a Developer API key for dev from your [CDP Portal profile](https://portal.cdp-int.defra.cloud/user-profile) in the `cdpApiKey` collection variable, in the **Current value** column |

Import the collection into Postman, then right-click a folder and choose **Run folder** to run every test. If
`cdpApiKey` is empty, or the gateway rejects it, the dev requests say so.

To run them from the command line:

```bash
npx newman run postman/waste-submit-reg-packaging-api.postman_collection.json --folder "1. Local"
npx newman run postman/waste-submit-reg-packaging-api.postman_collection.json --folder "2. CDP dev" --env-var cdpApiKey=<your key>
```

## Testing

```bash
dotnet test
```

The test project is foldered by type of test, then by module:

| Folder | What it holds | Needs Docker |
|---|---|---|
| `ArchitectureTests/` | Module boundaries and endpoint conventions | No |
| `UnitTests/` | Business rules, with no host and no database | No |
| `ServiceTests/` | The service running on a throwaway Postgres built from the Liquibase changelog, with the real handlers and the seeded data. `XxxEndpointsTest`: the app started in memory and called over HTTP | Yes |
| | `XxxDatabaseTest`: SQL, mapping and seeded data, with a handler called directly | Yes |
| `ContractTests/` | The v1 OpenAPI snapshot | No |
| `Support/` | Shared factories, fixtures and test data | |

Without Docker, the tests that need it are skipped rather than failed, which is what happens inside the Docker image
build. In GitHub Actions (`CI=true`) they fail instead, so a pull request can't pass with the database tests unrun.
To run one type, filter on its namespace: `dotnet test --filter "FullyQualifiedName~.Test.UnitTests."`.

Where a new test goes is in [CLAUDE.md](CLAUDE.md), "Testing". Checks against a deployed environment are not here:
they are in waste-submit-journey-tests and waste-submit-performance-tests.

## CI/CD

Builds run in GitHub Actions, and deployments happen in the [CDP Portal](https://portal.cdp-int.defra.cloud).

1. **Pull request to `main`.** [check-pull-request.yml](.github/workflows/check-pull-request.yml) runs `dotnet test`,
   and [check-schema.yml](.github/workflows/check-schema.yml) checks the Liquibase changelog against Postgres.
2. **Merge to `main`.** [publish.yml](.github/workflows/publish.yml) runs CDP's build action. It builds the Docker image
   (which runs the tests again), tags the commit with a new version and pushes the image to CDP. Each merge bumps the
   minor version, for example `0.2.0` to `0.3.0`. Put `#major` or `#patch` in a commit message to bump that part
   instead. The build action also publishes the image to DEFRA's public Docker Hub unless `push-dockerhub: false` is set.
   The same job publishes the Liquibase changelog, if it changed, ready to apply from the Portal (see
   [Schema changes in CDP](#schema-changes-in-cdp)).
3. **Deploy.** In the CDP Portal, choose **Deploy Service**, then the version and environment. Each version is built
   once, so promoting means deploying the same version to the next environment.
   - The service page's **Automations** tab can deploy every new version to dev, test or perf-test automatically.
   - Production is always deployed by hand. CDP does not allow automatic deployments to prod.
4. **Hotfix.** If prod needs a fix but `main` has moved on, [publish-hotfix.yml](.github/workflows/publish-hotfix.yml)
   builds a patch of an older version from a branch. See CDP's
   [Hotfix Builds](https://portal.cdp-int.defra.cloud/documentation/how-to/hotfix-builds.md) guide.

Not set up yet:

- **SonarCloud.** [sonarcloud.yml](.github/workflows/sonarcloud.yml) exists, but the jobs that call it are commented out
  in both workflows. It needs a SonarCloud project and a `SONAR_TOKEN` repository secret.
- **Dependabot.** Rename [.github/example.dependabot.yml](.github/example.dependabot.yml) to `.github/dependabot.yml` to
  turn it on.

## Project layout

| Path | Contains |
|---|---|
| `WasteSubmitRegPackaging.*/` | The modules (Registration, Packaging) and the shared kernel. See [docs/project_architecture.md](docs/project_architecture.md) |
| `changelog/` | The Liquibase changelog: one folder per module |
| `WasteSubmitRegPackagingApi/Obligations/` | The packaging endpoints, response models, the mapping from registration file codes to joiner and leaver events, and the stub data |
| `WasteSubmitRegPackagingApi/Config/` | Settings classes, validated at startup |
| `WasteSubmitRegPackagingApi/Utils/Postgres/` | The Postgres connection and its health check |
| `WasteSubmitRegPackagingApi/Utils/TrustStore.cs` | Loads the certificates CDP supplies, so TLS to Postgres works |
| `WasteSubmitRegPackagingApi/Utils/Logging/` | Logging in the format CDP requires |
| `WasteSubmitRegPackagingApi/Utils/Metrics/` | Request metrics, exported to CloudWatch for the custom dashboard |
| `WasteSubmitRegPackagingApi.Test/` | The tests, foldered by type of test (see [Testing](#testing)) |
| `postman/` | The Postman collection |
| `compose.yml`, `compose/` | Local containers |

## Not used yet

These came with the CDP template. They are left in place because we may need them, but nothing in the service uses them.

- **floci and Localstack.** These emulate AWS services such as S3 and SQS on port 4566, so code that uses them can run
  locally without a real AWS account. The `floci` container in `compose.yml` runs
  `compose/floci/start.d/10-setup-resources.sh` on start-up, but every line that would create a bucket or queue is
  commented out, so it creates nothing. `compose/aws.env` points the AWS SDK at it with dummy credentials.
  `compose/start-localstack.sh` is the older Localstack version of the same script.
- **Redis.** The `redis` container in `compose.yml` is only used by the commented-out frontend example in the same file.
  CDP only gives Redis to frontend services, so this API cannot use it in CDP.
- **Audit logging.** `Utils/Auditing/` is wired into the logging setup: anything logged with `logger.Audit(...)` goes to a
  separate audit stream. Nothing calls it yet.

## Licence

Open Government Licence v3. See [LICENSE](LICENSE).

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
