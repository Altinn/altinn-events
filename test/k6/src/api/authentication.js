import { check } from "k6";
import http from "k6/http";

import {
  buildHeaderWithBearer,
  buildHeaderWithContentType,
  buildHeaderWithCookie,
} from "../apiHelpers.js";
import { platformAuthentication, portalAuthentication, authCookieName } from "../config.js";
import { stopIterationOnFail, addErrorCount } from "../errorhandler.js";

const userName = __ENV.userName;
const userPassword = __ENV.userPassword;

export function exchangeToAltinnToken(token, test) {
  let endpoint = platformAuthentication.exchange + "?test=" + test;
  let params = buildHeaderWithBearer(token);

  let res = http.get(endpoint, params);
  let success = check(res, {
    "// Setup // Authentication towards Altinn 3 Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success);
  stopIterationOnFail(
    "// Setup // Authentication towards Altinn 3  Failed",
    success,
    res
  );

  return res.body;
}

export function authenticateUser() {
  if (!userName) {
    stopIterationOnFail(
      "Required environment variable username (userName) was not provided",
      false
    );
  }

  if (!userPassword) {
    stopIterationOnFail(
      "Required environment variable user password (userPassword) was not provided",
      false
    );
  }

  let endpoint = portalAuthentication.authenticateWithPwd;

  let requestBody = {
    UserName: userName,
    UserPassword: userPassword,
  };

  let params = buildHeaderWithContentType("application/json");

  let res = http.post(endpoint, JSON.stringify(requestBody), params);

  let success = check(res, {
    "// Setup // Authentication towards Altinn 2 Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success);
  stopIterationOnFail(
    "// Setup // Authentication towards Altinn 2 Success",
    success,
    res
  );

  let aspxAuthCookie = res.cookies[authCookieName][0].value;

  let refreshEndpoint = platformAuthentication.refresh;

  let headerParams = buildHeaderWithCookie(authCookieName, aspxAuthCookie);

  let res2 = http.get(refreshEndpoint, headerParams);
  let success2 = check(res2, {
    "// Setup // Authentication towards Altinn 3 Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success2);
  stopIterationOnFail(
    "// Setup // Authentication towards Altinn 3 Success",
    success2,
    res2
  );

  return res2.body;
}
