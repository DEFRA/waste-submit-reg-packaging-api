# waste-submit-reg-packaging-api

A .NET 10 API that serves aggregated packaging data from approved submissions to the Obligations service. It runs on
CDP in the protected zone, so it is reachable from other CDP services but not from the internet.

It currently serves **stub data**. The endpoints and response shapes are the agreed contract, so the Obligations team
can build against them while the real data source is wired in. The service is connected to its own Postgres database.

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
| `GET /packaging/{packagingYear}/producer-material-totals-by-submitter` | Every approved submission for the year, grouped by submitter, with each producer's material totals. Not paged |
| `GET /packaging/{packagingYear}/submitter-organisation/{submitterOrganisationId}/producer-material-totals` | The producers one submitter reported for: itself for a direct registrant, its members for a compliance scheme |
| `GET /health` | Whether the service is running |
| `GET /health/db` | Whether the service can reach Postgres |
| `GET /swagger`, `GET /openapi/v1.json` | The API documentation, where Swagger is turned on |

Both packaging endpoints return their results under `items`, which is empty when there is nothing for the year. The
submitter endpoint returns 404 only when the submitter organisation does not exist. For a compliance scheme,
`submitterOrganisationId` is the scheme's ID, not the organisation ID of the company that runs it. Each producer carries its
packaging in whole kilograms per material, and its `events`: every time it joined or left during the year, oldest first.

```json
"events": [
  { "code": 1, "action": "Joined", "date": "2025-03-03" },
  { "code": 13, "action": "Left", "date": "2025-07-14" }
]
```

`code` is numbered as in the
[GOV.UK registration file guidance](https://www.gov.uk/guidance/register-for-epr-for-packaging-create-your-registration-file),
and `action` (`Joined` or `Left`) is worked out from it. `events` is `[]` when the producer did neither.

The stub data is for year `2024`, with two submitters:

| Submitter | `submitterOrganisationId` |
|---|---|
| A direct registrant | `11111111-1111-4111-8111-111111111111` |
| A compliance scheme with four members: one with no changes, one joining, one leaving, and one joining, leaving and rejoining | `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` |

## Running locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

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
| `Swagger__Enabled` | Serve Swagger and the OpenAPI document | `true` in dev and test only |

A change in cdp-app-config does nothing until the service is redeployed from the CDP Portal.

## Postgres

The service has an Aurora PostgreSQL cluster (PostgreSQL 16) in every CDP environment, and connects to it with Npgsql.
CDP's guide is [Relational Databases on CDP](https://portal.cdp-int.defra.cloud/documentation/how-to/relational-databases.md).

### Postgres locally

`docker compose up -d postgres` starts PostgreSQL 16 on port 5432 with the same database and user names as CDP. It needs
no password. `appsettings.Development.json` points the service at it.

### Postgres in CDP

- **No password.** The service signs in with a 15-minute token from its IAM role, and fetches a new one every 10 minutes.
- **Two users.** The service uses `waste_submit_reg_packaging_api`, which can read and write data. Schema changes use
  `waste_submit_reg_packaging_api_ddl`. No tables exist yet; CDP's way of adding them is Liquibase changelogs applied from
  the Portal, which is not set up yet.
- **TLS.** `VerifyFull` checks the database's certificate against the RDS root certificates CDP supplies in
  `TRUSTSTORE_RDS_ROOT_CA`. If that ever fails, `Postgres__SslMode=Require` keeps the connection encrypted without
  checking the certificate.
- **Looking at the data.** Open a Terminal for the service in the CDP Portal and run `psql`, or
  `psql -U waste_submit_reg_packaging_api_ddl` for schema changes.

## Health checks

| Endpoint | Checks | Used by |
|---|---|---|
| `/health` | The service is running | CDP, to decide whether the container is alive |
| `/health/db` | A `SELECT 1` against Postgres | Us, to prove the database connection works |

`/health` leaves the database out on purpose. CDP restarts the container whenever `/health` fails, and a database blip
should not do that.

## Swagger

Swagger shows every endpoint, its parameters and its response shapes, and lets you call it from the browser.

### Swagger locally

1. Run the service (see [Running locally](#running-locally)).
2. Open http://localhost:8085/swagger.
3. Pick an endpoint, choose **Try it out**, fill in the parameters and choose **Execute**. Use year `2024` and the
   organisation IDs under [Endpoints](#endpoints).

The OpenAPI document behind the page is at http://localhost:8085/openapi/v1.json.

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
covers every endpoint: both health checks, both packaging endpoints (including the empty and 404 cases) and the API
documentation. It has two folders with the same requests and tests on every one.

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

The tests start the whole app in memory and call it over HTTP. One test connects to a real, throwaway Postgres in
Docker. Without Docker, that test is skipped rather than failed, which is what happens inside the Docker image build.

## CI/CD

Builds run in GitHub Actions, and deployments happen in the [CDP Portal](https://portal.cdp-int.defra.cloud).

1. **Pull request to `main`.** [check-pull-request.yml](.github/workflows/check-pull-request.yml) runs `dotnet test`.
2. **Merge to `main`.** [publish.yml](.github/workflows/publish.yml) runs CDP's build action. It builds the Docker image
   (which runs the tests again), tags the commit with a new version and pushes the image to CDP. Each merge bumps the
   minor version, for example `0.2.0` to `0.3.0`. Put `#major` or `#patch` in a commit message to bump that part
   instead. The build action also publishes the image to DEFRA's public Docker Hub unless `push-dockerhub: false` is set.
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
| `WasteSubmitRegPackagingApi/Obligations/` | The packaging endpoints, response models, the mapping from registration file codes to joiner and leaver events, and the stub data |
| `WasteSubmitRegPackagingApi/Config/` | Settings classes, validated at startup |
| `WasteSubmitRegPackagingApi/Utils/Postgres/` | The Postgres connection and its health check |
| `WasteSubmitRegPackagingApi/Utils/TrustStore.cs` | Loads the certificates CDP supplies, so TLS to Postgres works |
| `WasteSubmitRegPackagingApi/Utils/Logging/` | Logging in the format CDP requires |
| `WasteSubmitRegPackagingApi.Test/` | The tests, in the same folders as the code they test |
| `postman/` | The Postman collection |
| `compose.yml`, `compose/` | Local containers |

## Not used yet

These came with the CDP template. They are left in place because we may need them, but nothing in the service uses them.

- **MongoDB.** The connection setup (`Config/MongoConfig.cs`, `Utils/Mongo/`) and the `mongodb` container in
  `compose.yml`. In CDP a MongoDB is already set up for the service, and its connection details are set when it is
  deployed.
- **floci and Localstack.** These emulate AWS services such as S3 and SQS on port 4566, so code that uses them can run
  locally without a real AWS account. The `floci` container in `compose.yml` runs
  `compose/floci/start.d/10-setup-resources.sh` on start-up, but every line that would create a bucket or queue is
  commented out, so it creates nothing. `compose/aws.env` points the AWS SDK at it with dummy credentials.
  `compose/start-localstack.sh` is the older Localstack version of the same script.
- **Redis.** The `redis` container in `compose.yml` is only used by the commented-out frontend example in the same file.
  CDP only gives Redis to frontend services, so this API cannot use it in CDP.
- **HTTP client helpers.** `Utils/Http/` sets up outbound HTTP calls through CDP's proxy, ready for when the service
  calls something outside CDP.
- **Audit logging.** `Utils/Auditing/` is wired into the logging setup: anything logged with `logger.Audit(...)` goes to a
  separate audit stream. Nothing calls it yet.

## Licence

Open Government Licence v3. See [LICENSE](LICENSE).

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
