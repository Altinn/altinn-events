import http from "k6/http";
import encoding from "k6/encoding";

import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import * as apiHelpers from "../apiHelpers.js";
import { getFromSecretSource } from "../setup.js";

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

export async function generatePersonalToken() {

  let userId = __ENV.userId;
  let partyId = __ENV.partyId;
  let pid = await getFromSecretSource("pid");

  if (!userId) {
    stopIterationOnFail("Required environment variable user id (userId) was not provided", false);
  }

  if (!partyId) {
    stopIterationOnFail("Required environment variable party id (partyId) was not provided", false);
  }

  if (!pid) {
    stopIterationOnFail("Required environment variable person number (pid) was not provided", false);
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

  return await generateToken(endpoint);
}

async function generateToken(endpoint) {
  const tokenGeneratorUserName = await getFromSecretSource("tokenGeneratorUserName");
  const tokenGeneratorUserPwd = await getFromSecretSource("tokenGeneratorUserPwd");

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
