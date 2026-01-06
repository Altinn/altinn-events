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
  const endpoint = platformAuthentication.exchange + "?test=" + test;
  const params = buildHeaderWithBearer(token);

  const res = http.get(endpoint, params);
  const success = check(res, {
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

  const endpoint = portalAuthentication.authenticateWithPwd;

  const requestBody = {
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

  const aspxAuthCookie = res.cookies[authCookieName][0].value;

  const refreshEndpoint = platformAuthentication.refresh;

  params = buildHeaderWithCookie(authCookieName, aspxAuthCookie);

  res = http.get(refreshEndpoint, params);
  success = check(res, {
    "// Setup // Authentication towards Altinn 3 Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success);
  stopIterationOnFail(
    "// Setup // Authentication towards Altinn 3 Success",
    success,
    res
  );

  return res.body;
}
