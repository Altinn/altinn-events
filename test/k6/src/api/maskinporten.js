import { check } from "k6";
import encoding from "k6/encoding";
import http from "k6/http";

import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import { buildHeaderWithContentType } from "../apiHelpers.js";
import * as config from "../config.js";
import { stopIterationOnFail, addErrorCount } from "../errorhandler.js";

const encodedJwk = __ENV.encodedJwk;
const mpClientId = __ENV.mpClientId;
const mpKid = __ENV.mpKid;

export async function generateAccessToken(scopes) {
  if (!encodedJwk) {
    stopIterationOnFail("Required environment variable Encoded JWK (encodedJWK) was not provided", false);
  }

  if (!mpClientId) {
    stopIterationOnFail("Required environment variable maskinporten client id (mpClientId) was not provided", false);
  }

  if (!mpKid) {
    stopIterationOnFail("Required environment variable maskinporten kid (mpKid) was not provided", false);
  }

  const grant = await createJwtGrant(scopes);

  const body = {
    alg: "RS256",
    grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
    assertion: grant,
  };

  const res = http.post(config.maskinporten.token, body, buildHeaderWithContentType("application/x-www-form-urlencoded"));

  const success = check(res, {
    "// Setup // Authentication towards Maskinporten Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success);
  stopIterationOnFail(
    "// Setup // Authentication towards Maskinporten Failed",
    success,
    res
  );

  const accessToken = JSON.parse(res.body)['access_token'];
  return accessToken;
}

function base64urlEncode(obj) {
  return encoding.b64encode(JSON.stringify(obj), "rawurl");
}

async function createJwtGrant(scopes) {
  const header = {
    alg: "RS256",
    typ: "JWT",
    kid: mpKid,
  };

  const now = Math.floor(Date.now() / 1000);

  const payload = {
    aud: config.maskinporten.audience,
    scope: scopes,
    iss: mpClientId,
    iat: now,
    exp: now + 120,
    jti: uuidv4(),
  };

  const jwk = JSON.parse(encoding.b64decode(encodedJwk, "std", "s"));

  const cryptoKey = await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const signingInput = base64urlEncode(header) + "." + base64urlEncode(payload);
  const data = new TextEncoder().encode(signingInput);
  const signature = await crypto.subtle.sign("RSASSA-PKCS1-v1_5", cryptoKey, data);

  return signingInput + "." + encoding.b64encode(new Uint8Array(signature), "rawurl");
}
