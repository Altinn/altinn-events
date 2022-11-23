import * as tokenGenerator from "./api/token-generator.js";

const environment = __ENV.env.toLowerCase();

/*
 * generate an altinn token for TTD based on the environment using AltinnTestTools
 * @returns altinn token with the provided scopes for an org/appowner
 */
export function getAltinnTokenForTTD(scopes) {
  var queryParams = {
    env: environment,
    scopes: scopes,
    org: "ttd",
    orgNo: "991825827",
  };

  return tokenGenerator.generateEnterpriseToken(queryParams);
}