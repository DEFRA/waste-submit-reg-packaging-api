# waste-submit-reg-packaging-api

Core delivery C# ASP.NET backend template.

* [Install MongoDB](#install-mongodb)
* [Inspect MongoDB](#inspect-mongodb)
* [Postgres](#postgres)
* [Testing](#testing)
* [Running](#running)
* [Postman](#postman)
* [Swagger](#swagger)
* [Dependabot](#dependabot)


### Docker Compose

A Docker Compose template is in [compose.yml](compose.yml).

A local environment with:

- Localstack for AWS services (S3, SQS)
- Redis
- MongoDB
- Postgres
- This service.
- A commented out frontend example.

```bash
docker compose up --build -d
```

A more extensive setup is available in [github.com/DEFRA/cdp-local-environment](https://github.com/DEFRA/cdp-local-environment)

### MongoDB

#### MongoDB via Docker

See above.

```
docker compose up -d mongodb
```

#### MongoDB locally

Alternatively install MongoDB locally:

- Install [MongoDB](https://www.mongodb.com/docs/manual/tutorial/#installation) on your local machine
- Start MongoDB:
```bash
sudo mongod --dbpath ~/mongodb-cdp
```

#### MongoDB in CDP environments

In CDP environments a MongoDB instance is already set up
and the credentials exposed as enviromment variables.


### Inspect MongoDB

To inspect the Database and Collections locally:
```bash
mongosh
```

You can use the CDP Terminal to access the environments' MongoDB.

### Postgres

The service has an Aurora PostgreSQL cluster in every CDP environment, and connects to it with Npgsql.

#### Postgres locally

```bash
docker compose up -d postgres
```

`appsettings.Development.json` points at it. The local database needs no password.

#### Postgres in CDP environments

- There is no password. The service signs in with a 15-minute token from its IAM role, and fetches a new one every 10 minutes.
- The database and user are both `waste_submit_reg_packaging_api`. They and the port are in `appsettings.json`.
- The host is set per environment in cdp-app-config as `Postgres__Host`, the cluster's writer endpoint. The service will not start without it.
- TLS uses `VerifyFull` against the RDS root certificates CDP supplies in `TRUSTSTORE_RDS_ROOT_CA`. If that ever fails,
  `Postgres__SslMode=Require` keeps the connection encrypted without checking the certificate.
- To look at the data, open a Terminal for the service in the CDP Portal and run `psql`. Schema changes use
  `psql -U waste_submit_reg_packaging_api_ddl`.

#### Health checks

| Endpoint | Checks | Used by |
|---|---|---|
| `/health` | The service is running | CDP, to decide whether the container is alive |
| `/health/db` | A `SELECT 1` against Postgres | Us, to prove the database connection works |

`/health` leaves the database out on purpose. CDP restarts the container whenever `/health` fails, and a database blip should not do that.

### Testing

Run the tests with:

Tests run by running a full `WebApplication` backed by [Ephemeral MongoDB](https://github.com/asimmon/ephemeral-mongo).
Tests do not use mocking of any sort and read and write from the in-memory database.

```bash
dotnet test
````

### Running

Run CDP-Deployments application:
```bash
dotnet run --project WasteSubmitRegPackagingApi --launch-profile Development
```

### Postman

[postman/waste-submit-reg-packaging-api.postman_collection.json](postman/waste-submit-reg-packaging-api.postman_collection.json)
covers both packaging endpoints.

1. Start the service (see [Running](#running)). It listens on http://localhost:8085.
2. Import the collection into Postman.
3. Open **1. Local (stub data)** and send any request, or right-click the folder and choose **Run folder**
   to run every test.

The **2. CDP dev** folder needs the AWS OpenVPN and your Developer API key (CDP Portal, your profile) in the
`cdpApiKey` variable.

To run the local tests from the command line:

```bash
npx newman run postman/waste-submit-reg-packaging-api.postman_collection.json --folder "1. Local (stub data)"
```

### Swagger

Swagger shows every endpoint, its parameters and its response shapes, and lets you call it from the browser.

#### Swagger locally

1. Run the service (see [Running](#running)).
2. Open http://localhost:8085/swagger.
3. Pick an endpoint, choose **Try it out**, fill in the parameters and choose **Execute**. The stub data is for year `2024`.
   For the organisation endpoint, use `11111111-1111-4111-8111-111111111111` (a direct registrant) or
   `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` (a compliance scheme).

The OpenAPI document behind the page is at http://localhost:8085/openapi/v1.json.

#### Swagger in CDP environments

Swagger is on wherever cdp-app-config sets `Swagger__Enabled=true` (dev and test), and off everywhere else, including prod.

This is a protected-zone service, so a laptop can only reach it through the
[developer API key route](https://portal.cdp-int.defra.cloud/documentation/how-to/developer-api-key.md). Every request needs
an `x-api-key` header, which a browser will not send, so use the OpenAPI document rather than the page. With the AWS OpenVPN
connected and a Developer API key from your CDP Portal profile:

```bash
curl -H "x-api-key: $CDP_API_KEY" \
  https://ephemeral-protected.api.dev.cdp-int.defra.cloud/waste-submit-reg-packaging-api/openapi/v1.json \
  -o waste-submit-reg-packaging-api.openapi.json
```

That file is the contract for whatever is deployed. Import it into Postman (**Import**, then the file) or any OpenAPI tool,
or send it to a team that will call the API.

### SonarCloud

Example SonarCloud configuration are available in the GitHub Action workflows.

### Dependabot

We have added an example dependabot configuration file to the repository. You can enable it by renaming
the [.github/example.dependabot.yml](.github/example.dependabot.yml) to `.github/dependabot.yml`


### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
