import * as tokenGenerator from "./api/token-generator.js";
import * as maskinporten from "./api/maskinporten.js";
import * as authentication from "./api/authentication.js";
import { b64decode } from "k6/encoding";
import { stopIterationOnFail } from "./errorhandler.js";
import secrets from "k6/secrets";

const environment = (__ENV.altinn_env || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)

export async function getFromSecretSource(secretName) {
    let secretValue;
    try {
        secretValue = await secrets.get(secretName);
    }
    catch (error) {
        if (error.message == "no secret sources are configured") {
            stopIterationOnFail("The secret source is not configured", false);
        }
        else if (error.message == "no value") {
            stopIterationOnFail(`Secret ${secretName} does not exist in the secret source`, false);
        }
        console.log(error);
        stopIterationOnFail("Unknown error occurred in the attempt to get secret from source", false);
    }
    if (!secretValue) {
        stopIterationOnFail(`Secret ${secretName} is not properly assigned in the secret source`, false);
    }
    return secretValue;
}

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

  return await tokenGenerator.generatePersonalToken();
}

export function getPartyIdFromTokenClaim(jwtToken) {
  const parts = jwtToken.split(".");
  let claims = JSON.parse(b64decode(parts[1].toString(), "rawstd", "s"));

  return claims["urn:altinn:partyid"];
}
