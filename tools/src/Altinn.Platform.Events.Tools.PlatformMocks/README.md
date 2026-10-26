# PlatformMocks

WireMock.Net servers for the Altinn platform APIs the Events API calls, and a webhook receiver to use as subscription endpoint. The [Aspire app host](../../aspire/README.md) starts them as `wiremock-platform`. Every request and response is logged with its body, under the caller's trace.

| System | Port | Endpoints |
| :--- | :--- | :--- |
| `register` | 5082 | `POST /register/api/v1/parties/lookup`, `POST /register/api/v2/internal/parties/query`, `POST /register/api/v2/internal/parties/main-units` |
| `authorization` | 5083 | `POST /authorization/api/v1/decision` |
| `webhooks` | 5084, https | `POST /webhooks/{scenario}/{anything}` |

Each system has `Enabled` and `Port` under `PlatformMocksSettings:<System>`.

## Register

Every organisation and person exists.

* The parties in `KnownParties` keep their ids. `appsettings.json` has the ones from the Bruno test data, which are tt02 party ids.
* Other organisations get party id `500000000 + organisation number`, so a lookup by party id finds the organisation again.
* Other persons get a stable id made from the national identity number. A lookup by that party id finds them once they have been looked up by number.
* `MainUnits` maps a subunit's organisation number to its main unit's. Other organisations have no main unit.

## Authorization

Permits every request in a XACML multi-request, except when a subject attribute has a value in `DeniedSubjects`, e.g. a user id or an org code. Each result has the attributes the request marked with `IncludeInResult`, like the real PDP, since the API matches the results to consumers and events by them.

## Webhooks

Serves https, since the API only accepts https subscription endpoints. `Webhooks:CertificatePath` and `CertificatePassword` point to a PKCS#12 file. The app host uses the exported ASP.NET Core development certificate.

| Path | Answer |
| :--- | :--- |
| `/webhooks/fail/...` | 503 |
| `/webhooks/slow/...` | 200 after `SlowSeconds` |
| `/webhooks/unvalidated/...` | 400 to the validation event (`platform.events.validatesubscription`), 200 to the rest |
| any other `/webhooks/...` | 200 |
