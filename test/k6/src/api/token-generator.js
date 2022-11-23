import http from "k6/http";
import { check } from "k6";
import encoding from "k6/encoding";

import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";

const tokenGeneratorUserName = __ENV.tokenGeneratorUserName;
const tokenGeneratorUserPwd = __ENV.tokenGeneratorUserPwd;

/*
Generate enterprise token for test environment
*/
export function generateEnterpriseToken(queryParams) {
  var success;
  const credentials = `${tokenGeneratorUserName}:${tokenGeneratorUserPwd}`;
  const encodedCredentials = encoding.b64encode(credentials);

  var endpoint =
    config.tokenGenerator.getEnterpriseToken +
    buildQueryParametersForEndpoint(queryParams);

  var params = {
    headers: {
      Authorization: `Basic ${encodedCredentials}`,
    },
  };

  var response = http.get(endpoint, params);

  if (response.status != 200) {
    stopIterationOnFail("Enterprise token generation failed", false, response);
  }

  var token = response.body;
  return token;
}

/*
Build query parameters
*/
function buildQueryParametersForEndpoint(filterParameters) {
  var query = "?";
  Object.keys(filterParameters).forEach(function (key) {
    if (Array.isArray(filterParameters[key])) {
      filterParameters[key].forEach((value) => {
        query += key + "=" + value + "&";
      });
    } else {
      query += key + "=" + filterParameters[key] + "&";
    }
  });
  query = query.slice(0, -1);

  return query;
}
