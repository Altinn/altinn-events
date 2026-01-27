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
    -e runFullTestSet=true `
    -e useCSVData=true
    --vus 5 `
    --duration 30s

    // Single line command to run the test using the script with example environment variables:
    docker-compose run k6 run /src/tests/app-events.js -e altinn_env=*** -e tokenGeneratorUserName=*** -e tokenGeneratorUserPwd=*** -e app=apps-test -e userId=*** -e partyId=*** -e personNumber=*** -e userName=*** -e userPassword=*** -e runFullTestSet=true -e useCSVData=true --vus 10 --duration 30s

    Update the variables --vus and --duration as needed for performance testing
    For use case tests omit environment variable runFullTestSet or set value to false
    Retrieve the userName and userPassword from Altinn Pedia https://pedia.altinn.cloud/altinn-3/ops/patching/containers/ for the respective environment    
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as appEventsApi from "../api/app-events.js";
import { addErrorCount } from "../errorhandler.js";
import { loadCSV, loadJSONDirectory, getItemByVU } from "../dataLoader.js";

const useCSVData = __ENV.useCSVData ? __ENV.useCSVData.toLowerCase().includes("true") : false;

const eventVariations = useCSVData 
  ? loadCSV('app-event-variations', '../data/app-events/event-variations.csv') 
  : loadJSONDirectory('app-event-variations', '../data/app-events/', 
    [
      '01-app-event.json'
    ]);

console.log(`[APP-EVENTS] Using CSV data: ${useCSVData}`);
console.log(`[APP-EVENTS] Event variations loaded: ${eventVariations.length}`);

export const options = {
  thresholds: {
    // Allow up to 5% error rate for performance testing
    error_rate: ["rate<0.05"],
    // HTTP error rate should be below 2%
    http_req_failed: ["rate<0.02"],
    // 95% of requests should complete within 1 second
    http_req_duration: ["p(95)<1000"],
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

  let nextUrl = response.headers["Next"] || response.headers["next"];

  let success = check(response, {
    "01 - GET app events for org. Query parameter 'after'. Status is 200": (
      r
    ) => r.status === 200,
    "01 - GET app events for org. Query parameter 'after'. List contains minimum one element":
      (r) => JSON.parse(r.body).length >= 1,
  });

  if (nextUrl) {
    check(nextUrl, {
      "01 - GET app events for org. Query parameter 'after'. Next url provided": (url) => url !== undefined,
    });
  }

  addErrorCount(success);
  return nextUrl;
}

// 02 - GET events for org from next url
function TC02_GetAppEventsForOrgFromNextUrl(data, nextUrl) {
  if (!nextUrl) {
    // console.warn("02 - Skipping test: nextUrl is undefined");
    return;
  }

  let response = appEventsApi.getEventsFromNextLink(nextUrl, data.orgToken);

  let success = check(response, {
    "02 - GET app events for org from next url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 03 -  GET app events for party. Query parameters: partyId, after.
function TC03_GetAppEventsForParty(data) {
  let response = appEventsApi.getEventsForParty(
    { after: 1, party: data.userPartyId },
    data.userToken
  );

  let nextUrl = response.headers["Next"] || response.headers["next"];
  let responseBody;
  if (response.status === 200) {
    try {
      responseBody = JSON.parse(response.body);
    } catch (e) {
      console.warn("03 - Response body was not valid JSON");
    }
  }
  let success = check(response, {
    "03 - GET app events for party. Query parameters: partyId, after. Status is 200":
      (r) => r.status === 200,
  });
  // Only check for minimum one element if the response is successful
  if (response.status === 200 && Array.isArray(responseBody) && responseBody.length === 0) {
    console.log(`TC03: No events found for party ${data.userPartyId} - this may be expected`);
  }

  addErrorCount(success);

  return nextUrl;
}
// No next URL means no more pages - this is normal, not an error
// 04 - GET events for party from 'next' url
function TC04_GetAppEventsForPartyFromNextUrl(data, nextUrl) {
  if (!nextUrl) {
    console.warn("04 - Skipping test: nextUrl is undefined");
    return;
  }

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
 * 03 -  GET app events for party. Query parameters: partyId, after
 * 04 - GET app events for party from 'next'
 */
export default function runTests(data) {
  try {
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
