/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/app-events.js `
    -e env=*** `
    -e tokenGeneratorUserName=*** `
    -e tokenGeneratorUserPwd=*** `
    -e app=apps-test `
    -e userId=*** `
    -e partyId=*** `
    -e personNumber=*** `
    -e runFullTestSet=true
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as appEventsApi from "../api/app-events.js";
import { generateJUnitXML, reportPath } from "../report.js";
import { addErrorCount } from "../errorhandler.js";

export function setup() {
  var scopes = "altinn:serviceowner/instances.read";
  const app = __ENV.app.toLowerCase();
  const org = "ttd";
  const partyId = __ENV.partyId;

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  var userToken = setupToken.getAltinnTokenForUser(
    __ENV.userId,
    partyId,
    __ENV.personNumber
  );

  var orgToken = setupToken.getAltinnTokenForOrg(scopes, org);

  var data = {
    runFullTestSet: runFullTestSet,
    userToken: userToken,
    userPartyId: partyId,
    orgToken: orgToken,
    org: org,
    app: app,
  };

  return data;
}

// 01 - GET events for org. Query parameter 'after'
function TC01_GetAppEventsForOrg(data) {
  var response, success;

  response = appEventsApi.getEventsForOrg(
    data.org,
    data.app,
    { after: 1 },
    data.orgToken
  );

  var nextUrl = response.headers["Next"];

  success = check(response, {
    "01 - GET app events for org. Query parameter 'after'. Status is 200": (
      r
    ) => r.status === 200,
    "01 - GET app events for org. Query parameter 'after'. List contains minimum one element":
      (r) => JSON.parse(r.body).length >= 1,
    "01 - GET app events for org. Query parameter 'after'. Next url provided":
      nextUrl,
  });

  addErrorCount(success);
  return nextUrl;
}

// 02 - GET events for org from next url
function TC02_GetAppEventsForOrgFromNextUrl(data, nextUrl) {
  var response, success;
  response = appEventsApi.getEventsFromNextLink(nextUrl, data.orgToken);

  success = check(response, {
    "02 - GET app events for org from next url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 03 -  GET app events for party. Query parameters: partyId, after.
function TC03_GetAppEventsForParty(data) {
  var response, success;

  response = appEventsApi.getEventsForParty(
    { after: 1, party: data.userPartyId },
    data.userToken
  );

  var nextUrl = response.headers["Next"];

  success = check(response, {
    "03 - GET app events for party. Query parameters: partyId, after. Status is 200":
      (r) => r.status === 200,
    "03 - GET app events for party. Query parameters: partyId, after. List contains minimum one element":
      (r) => JSON.parse(r.body).length >= 1,
    "03 - GET app events for party. Query parameters: partyId, after. Next url provided":
      nextUrl,
  });

  addErrorCount(success);

  return nextUrl;
}

// 04 - GET events for party from 'next' url
function TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl) {
  var response, success;

  response = appEventsApi.getEventsFromNextLink(nextUrl, data.userToken);

  success = check(response, {
    "04 - GET app events for party from 'next' url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

/*
 * 01 - GET app events for org. Query parameter 'after'
 * 02 - GET app  events for org from 'next' url
 * 03 -  GET app events for party. Query parameters: partyId, after
 * 04 - GET app events for party from 'next'
 */
export default function (data) {
  if (data.runFullTestSet) {
    let nextUrl = TC01_GetAppEventsForOrg(data);

    TC02_GetAppEventsForOrgFromNextUrl(data, nextUrl);

    nextUrl = TC03_GetAppEventsForParty(data);

    TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl);
  } else {
    // Limited test set for use case tests
    let nextUrl = TC03_GetAppEventsForParty(data);

    TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl);
  }
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "platform-events");
  return result;
}
*/
