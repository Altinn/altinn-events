import * as tokenGenerator from "./api/token-generator.js";
import * as maskinporten from "./api/maskinporten.js";
import * as authentication from "./api/authentication.js";
import { b64decode } from "k6/encoding";

const environment = (__ENV.altinn_env || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)

/*
 * generate an altinn token for org based on the environment using AltinnTestTools
 * If org is not provided TTD will be used.
 * @returns altinn token with the provided scopes for an org
 */
export function getAltinnTokenForOrg(scopes, org = "ttd", orgNo = "991825827") {
  if ((environment == "prod" || environment == "tt02") && org == "ttd") {
    let accessToken = maskinporten.generateAccessToken(scopes);
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

export function getAltinnTokenForUser() {
  if (environment == "prod" || environment == "tt02") {
    return authentication.authenticateUser();
  }

  return tokenGenerator.generatePersonalToken();
}

export function getPartyIdFromTokenClaim(jwtToken) {
  const parts = jwtToken.split(".");
  let claims = JSON.parse(b64decode(parts[1].toString(), "rawstd", "s"));

  return claims["urn:altinn:partyid"];
}
