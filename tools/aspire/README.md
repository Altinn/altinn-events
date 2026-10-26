# Aspire app host

Runs the Events API locally from one command, with everything it calls as local containers or mocks, so nothing needs at22, Azure or a secret. The Aspire dashboard shows the logs from all of them.

| Resource | What | Port |
| :--- | :--- | :--- |
| `api` | The Events API (`src/Events`), with the outbound and validation listeners turned on, so it delivers events itself | 5080 |
| `postgres` | PostgreSQL with the roles and database from the main README. Persistent: the container and its volume `altinn-events-aspire-db` survive a restart | 5440 |
| `servicebus` | The Service Bus emulator with the queues from `emulator/config.json`. Persistent | assigned by Aspire |
| `keyvault` | [Lowkey Vault](https://github.com/nagyesta/lowkey-vault) in place of Azure Key Vault, with the platform access token certificates | 5085, 5086 |
| `mock-auth` | Token issuer in place of the Altinn test token generator and the OpenID endpoints | 5081 |
| `wiremock-platform` | Register, Authorization (PDP) and a webhook receiver for subscriptions | 5082, 5083, 5084 |

The API's code and appsettings are not changed. The app host sets everything it needs through environment variables.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Aspire CLI](https://aspire.dev/get-started/install-cli/)
* Docker or [Podman](https://podman.io/). For Podman, set `ASPIRE_CONTAINER_RUNTIME=podman`.
* **A trusted ASP.NET Core development certificate:** `dotnet dev-certs https --trust`. The vault and the webhook receiver serve https with it, because the Key Vault SDK and the API's subscription endpoints only accept https.
  * On Linux, .NET only trusts it when `~/.aspnet/dev-certs/trust` is in `SSL_CERT_DIR`, as `dotnet dev-certs` says, e.g. `export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"`.
* **`events.dev.localhost` must resolve to 127.0.0.1.** Many Linux distributions do this for every `*.localhost` name. Otherwise add this line to `/etc/hosts` (on Windows, `C:\Windows\System32\drivers\etc\hosts`):

  ```
  127.0.0.1 events.dev.localhost
  ```

  The app host stops with this message if the name does not resolve. .NET 11 resolves `*.localhost` itself.

## Run

```bash
cd tools/aspire
aspire run
```

The dashboard opens on `https://localhost:17194`. To skip its certificate, use the `http` profile: `aspire run -lp http`, dashboard on `http://localhost:15194`.

The first run pulls the images, which takes a few minutes. `postgres` and `servicebus` keep running when the app host stops, so the next start is quick. To start from scratch, remove the containers and the `altinn-events-aspire-db` volume.

## Try it

Tokens come from `mock-auth` with basic auth `mock`/`mock`, with the same endpoints and parameters as the Altinn test token generator.

```bash
A=http://localhost:5081
API=http://localhost:5080/events/api/v1
SUB=$(curl -s -u mock:mock "$A/api/GetEnterpriseToken?org=ttd&orgNo=991825827&scopes=altinn:events.subscribe")
PUB=$(curl -s -u mock:mock "$A/api/GetEnterpriseToken?org=ttd&orgNo=991825827&scopes=altinn:events.publish")

# Subscribe with the webhook receiver as endpoint
curl -H "Authorization: Bearer $SUB" -H "Content-Type: application/json" \
  -d '{"resourceFilter":"urn:altinn:resource:ttd-altinn-events-automated-tests","endpoint":"https://localhost:5084/webhooks/ok/my-test"}' \
  $API/subscriptions

# Publish an event
curl -H "Authorization: Bearer $PUB" -H "Content-Type: application/cloudevents+json" \
  -d '{"specversion":"1.0","id":"'$(uuidgen)'","type":"automated.test.created","source":"https://example.com/my-test","subject":"urn:altinn:organization:identifier-no:311443755","resource":"urn:altinn:resource:ttd-altinn-events-automated-tests"}' \
  $API/events
```

The `wiremock-platform` log then shows the validation event and the event arriving at `/webhooks/ok/my-test`, after the API's calls to Register and Authorization.

### Webhook receiver

Use `https://localhost:5084/webhooks/{scenario}/{anything}` as a subscription's endpoint. Every request and its body is in the `wiremock-platform` log.

| Scenario | Answer |
| :--- | :--- |
| `ok`, or anything not below | 200 |
| `fail` | 503, so the API retries by `WolverineSettings:OutboundQueuePolicy` and then dead-letters |
| `slow` | 200 after 10 seconds (`PlatformMocksSettings:Webhooks:SlowSeconds`) |
| `unvalidated` | 400 to the validation event, so the subscription is never validated, and 200 to the rest |

### Platform access tokens

`GET /api/GetPlatformAccessToken?app=events` gives a token from `platform`, and `?org=ttd&app=apps-test` one from the app. The API checks them against `<issuer>-access-token-public-cert` in the vault, like in the cluster, so a token signed with another key gets 401. The certificates are the test certificates in `test/Altinn.Platform.Events.Tests`:

| Where | What | Used by |
| :--- | :--- | :--- |
| Vault secrets `platform-access-token-public-cert` and `ttd-access-token-public-cert` | Public certificates | The API, to check incoming access tokens |
| Vault certificate `platform-access-token-private-cert` | Certificate with private key | Events.Functions, to sign its calls to the API |
| `src/accesstoken/accesstokencredentials.pfx` (git-ignored) | Certificate with private key | The API, to sign its calls to Register. `Altinn.Common.AccessTokenClient` reads it from `<parent of the working folder>/accesstoken/` |

### Bruno

Copy `test/bruno/aspire.env` to `test/bruno/.env` and use the `Local` environment. `Token generator`, `Events publish`, `Events search` and `Subscriptions/generic subscription (org)` work against the stack.

* The app subscriptions have sources on `apps.at22.altinn.cloud`, and the API only accepts its own `AppsDomain`, `apps.altinn.no` in Development, so they get 400. With `https://ttd.apps.altinn.no/ttd/apps-test` they work.
* `Subscriptions/generic subscription (org)` has a webhook.site endpoint, so the API sends the validation event there. Use `https://localhost:5084/webhooks/...` to keep it local.
* The access token requests save the token in `environments/Local.yml` with `bru.setEnvVar`. Don't commit that.

## How Key Vault works locally

The API reads secrets from Key Vault as configuration (`kvSetting:SecretUri`) and gets the public access token certificates from it. Functions gets its signing certificate from it. All of them use `DefaultAzureCredential` and the Key Vault SDK as they are, which requires:

* **https.** Lowkey Vault serves https with the development certificate, which the app host exports to `.local/` (git-ignored) on each start.
* **A vault host name that ends with `.<resource in the vault's auth challenge>`.** Lowkey Vault answers with `dev.localhost` (`LOWKEY_AUTH_RESOURCE`), and the vault is at `events.dev.localhost`, which the development certificate covers (`*.dev.localhost`).
* **A token.** The API gets `IDENTITY_ENDPOINT`/`IDENTITY_HEADER`, so `DefaultAzureCredential` uses managed identity and gets its token from Lowkey Vault's endpoint on 5086.

## Settings

Override `appsettings.json` in a git-ignored `appsettings.Local.json`.

| Setting | Default | Description |
| :--- | :--- | :--- |
| `Api:SubscriptionCachingLifetimeInSeconds` | `5` | The API caches subscriptions and authorization decisions for an hour by default, so a new subscription would not match events for that long |
| `Ports:*` | see the table at the top | Host ports for the mocks and containers. The API's 5080 is fixed, since its settings refer to it |

## Not covered yet

* **Events.Functions** and **Azurite**. The API's `inbound` and `outbound` endpoints are the path Functions uses, and put messages on Storage Queues, which fails without Azurite. Events published through `events` and `app` go through Service Bus and are delivered. The vault already has the certificate Functions signs with.
* **Telemetry.** The API only exports to Azure Monitor, so the dashboard has its console logs but no traces or metrics.
* A control panel that shows the events the webhook receiver gets and answers requests by hand, like in altinn-notifications.
