import * as tokenGenerator from "./api/token-generator.js";
import * as maskinporten from "./api/maskinporten.js";
import * as authentication from "./api/authentication.js";
import { b64decode } from "k6/encoding";
import { getFromSecretSource } from "./secret-reader.js";
import { addErrorCount, stopIterationOnFail } from "./errorhandler.js";
import { platformAuthentication } from "./config.js";
import { check } from "k6";
import http from "k6/http";

const environment = (__ENV.altinn_env || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)

/*
 * generate an altinn token for org based on the environment using AltinnTestTools
 * If org is not provided TTD will be used.
 * @returns altinn token with the provided scopes for an org
 */
export async function getAltinnTokenForOrg(scopes, org = "ttd", orgNo = "991825827") {
  if ((environment == "prod" || environment == "tt02") && org == "ttd") {
    let accessToken = await maskinporten.generateAccessToken(scopes);
    return authentication.exchangeToAltinnToken(accessToken, true);
  }

  let queryParams = {
    env: environment,
    scopes: scopes.replace(/ /gi, ","),
    org: org,
    orgNo: orgNo,
  };

  return tokenGenerator.generateEnterpriseToken(queryParams);
}

export async function getAltinnTokenForUser() {
  if (environment == "prod") {
    return authentication.authenticateUser();
  }

  return await loginWithMockporten();
}

export function getPartyIdFromTokenClaim(jwtToken) {
  const parts = jwtToken.split(".");
  let claims = JSON.parse(b64decode(parts[1].toString(), "rawstd", "s"));

  return claims["urn:altinn:partyid"];
}

/*
 * Logs in an end user via Mockporten (test IDP); returns the runtime token.
 * pid must be a synthetic Tenor fødselsnummer (month 81-92). Never log res.url.
 */
export async function loginWithMockporten() {

  const pid = await getFromSecretSource("pid");
  const testidppwd = await getFromSecretSource("testidppwd");

  http.cookieJar().clear(platformAuthentication.refresh);
  let endpoint = platformAuthentication.refresh + "&iss=mockporten";
  let res = http.get(endpoint);
  let success = check(res, { "Mockporten login form loaded": (r) => r.status === 200 });
  addErrorCount(success);
  stopIterationOnFail("Mockporten login form not loaded", success, res);

  res = res.submitForm({ fields: { Pid: pid, Password: testidppwd } });
  success = check(res, { "Mockporten authentication success": (r) => r.status === 200 });
  addErrorCount(success);
  stopIterationOnFail("Mockporten authentication failed", success, res);
  return res.body;
}