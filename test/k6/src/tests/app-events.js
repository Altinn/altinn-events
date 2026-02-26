/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/app-events.js `
    -e env=*** `
    -e tokenGeneratorUserName=*** `
    -e tokenGeneratorUserPwd=*** `
    -e mpClientId=*** `
    -e mpKid=altinn-usecase-events `
    -e encodedJwk=*** `
    -e app=apps-test `
    -e userId=*** `
    -e partyId=*** `
    -e personNumber=*** `
    -e userName=*** `
    -e userPassword=*** `
    -e runFullTestSet=true
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as appEventsApi from "../api/app-events.js";
import { addErrorCount } from "../errorhandler.js";
import { loadCSV, loadJSONDirectory } from "../dataLoader.js";

const useCSVData = __ENV.useCSVData ? __ENV.useCSVData.toLowerCase().includes("true") : false;

console.log(`[APP-EVENTS] Using CSV data: ${useCSVData}`);

export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

export function setup() {
  let scopes = "altinn:serviceowner";
  const app = __ENV.app.toLowerCase();
  const org = "ttd";
  let partyId = __ENV.partyId;

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  let userToken = setupToken.getAltinnTokenForUser();

  if (!partyId) {
    partyId = setupToken.getPartyIdFromTokenClaim(userToken);
  }

  let orgToken = setupToken.getAltinnTokenForOrg(scopes, org);

  let data = {
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
  let response = appEventsApi.getEventsForOrg(
    data.org,
    data.app,
    { after: 1 },
    data.orgToken
  );

  let nextUrl = response.headers["Next"];

  let success = check(response, {
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
  let response = appEventsApi.getEventsFromNextLink(nextUrl, data.orgToken);

  let success = check(response, {
    "02 - GET app events for org from next url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 03 -  GET app events for party. Query parameters: partyId, from.
function TC03_GetAppEventsForParty(data) {
  const today = new Date();
  const sevenDaysAgo = new Date(today.setDate(today.getDate() - 7));

  let response = appEventsApi.getEventsForParty(
    { from: sevenDaysAgo.toISOString(), party: data.userPartyId },
    data.userToken
  );

  let nextUrl = response.headers["Next"];

  let success = check(response, {
    "03 - GET app events for party. Query parameters: partyId, from. Status is 200":
      (r) => r.status === 200,
    "03 - GET app events for party. Query parameters: partyId, from. List contains minimum one element":
      (r) => JSON.parse(r.body).length >= 1,
    "03 - GET app events for party. Query parameters: partyId, from. Next url provided":
      nextUrl,
  });

  addErrorCount(success);

  return nextUrl;
}

// 04 - GET events for party from 'next' url
function TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl) {
  let response = appEventsApi.getEventsFromNextLink(nextUrl, data.userToken);

  let success = check(response, {
    "04 - GET app events for party from 'next' url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

/*
 * 01 - GET app events for org. Query parameter 'after'
 * 02 - GET app  events for org from 'next' url
 * 03 - GET app events for party. Query parameters: partyId, from
 * 04 - GET app events for party from 'next'
 */
export default function runTests(data) {
  try {
    if (data.runFullTestSet) {
      let nextUrl = TC01_GetAppEventsForOrg(data);

      TC02_GetAppEventsForOrgFromNextUrl(data, nextUrl);

      nextUrl = TC03a_GetAppEventsForParty_From(data);
      TC03b_GetAppEventsForParty_After1(data);

      TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl);
    } else {
      // Limited test set for use case tests
      let nextUrl = TC03_GetAppEventsForParty(data);
      TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl);
    }
  } catch (error) {
    addErrorCount(false);
    throw error;
  }
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "platform-events");
  return result;
}
*/
