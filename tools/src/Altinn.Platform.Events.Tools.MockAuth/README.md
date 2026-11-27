# MockAuth

A local token issuer in place of the Altinn test token generator, so the Events API can be called without at22 credentials. The [Aspire app host](../../aspire/README.md) starts it as `mock-auth` and points the API to its OpenID configuration.

All token endpoints take basic auth `mock`/`mock` (`MockAuthSettings:Username`/`Password`) and the same query parameters as the test token generator, so the Bruno requests in `test/bruno/Token generator` work with `TOKEN_URL=http://localhost:5081`.

| Endpoint | Token |
| :--- | :--- |
| `GET /api/GetEnterpriseToken?org=&orgNo=&scopes=` | Service owner token: `urn:altinn:org`, `urn:altinn:orgNumber`, the scopes space-separated in `urn:altinn:scope` |
| `GET /api/GetPersonalToken?userId=&partyId=&pid=&authLvl=&scopes=` | End user token: `urn:altinn:userid`, `urn:altinn:partyid`, `pid`, the scopes in `scope` |
| `GET /api/GetSelfRegisteredEmailUserToken?userId=&partyId=&email=&scopes=` | End user token without `pid`, with `email` |
| `GET /api/GetPlatformAccessToken?org=&app=` | Platform access token with issuer `org` (default `platform`) and `urn:altinn:app` |
| `GET /authentication/api/v1/openid/.well-known/openid-configuration`, `/jwks` | OpenID discovery and the public key the bearer tokens are signed with |

Scopes may be separated by commas or spaces.

## Certificates

All are the test certificates from `test/Altinn.Platform.Events.Tests`, linked into the output:

* Bearer tokens are signed with `jwtselfsignedcert.pfx`, published as JWKS.
* Platform access tokens are signed with `platform-org.pfx` (issuer `platform`) or `ttd-org.pfx` (issuer `ttd`). The API checks them against `<issuer>-access-token-public-cert` in Key Vault, where the app host puts the public part.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Platform.Events.Tools.MockAuth
```

It listens on `http://localhost:5081` (`MockAuthSettings:Port`).
