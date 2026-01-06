import http from "k6/http";
import encoding from "k6/encoding";

import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import * as apiHelpers from "../apiHelpers.js";

const tokenGeneratorUserName = __ENV.tokenGeneratorUserName;
const tokenGeneratorUserPwd = __ENV.tokenGeneratorUserPwd;
const environment = (__ENV.altinn_env || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)

/*
Generate enterprise token for test environment
*/
export function generateEnterpriseToken(queryParams) {
  let endpoint =
    config.tokenGenerator.getEnterpriseToken +
    apiHelpers.buildQueryParametersForEndpoint(queryParams);

  return generateToken(endpoint);
}

export function generatePersonalToken() {

  let userId = __ENV.userId;
  let partyId = __ENV.partyId;
  let pid = __ENV.personNumber

  if (!userId) {
    stopIterationOnFail("Required environment variable user id (userId) was not provided", false);
  }

  if (!partyId) {
    stopIterationOnFail("Required environment variable party id (partyId) was not provided", false);
  }

  if (!pid) {
    stopIterationOnFail("Required environment variable person number (personNumber) was not provided", false);
  }

  let queryParams = {
    env: environment,
    userId: userId,
    partyId: partyId,
    pid: pid,
  };

  let endpoint =
    config.tokenGenerator.getPersonalToken +
    apiHelpers.buildQueryParametersForEndpoint(queryParams);

  return generateToken(endpoint);
}

function generateToken(endpoint) {
  const credentials = `${tokenGeneratorUserName}:${tokenGeneratorUserPwd}`;
  const encodedCredentials = encoding.b64encode(credentials);

  let params = apiHelpers.buildHeaderWithBasic(encodedCredentials);

  let response = http.get(endpoint, params);

  if (response.status != 200) {
    stopIterationOnFail("Token generation failed", false, response);
  }

  let token = response.body;
  return token;
}
