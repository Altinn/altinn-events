/*
    Test script to platform events api with user token
    Command: docker-compose run k6 run /src/tests/app-events.js -e tokenGeneratorUserName=autotest -e tokenGeneratorUserPwd=*** -e env=*** -e app=apps-test -e userId=20000000 -e partyId=01014922047 -e pid=01014922047
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

  var userToken = setupToken.getAltinnTokenForUser(
    __ENV.userId,
    partyId,
    __ENV.pid
  );
  var orgToken = setupToken.getAltinnTokenForOrg(scopes, org);

  var data = {
    userToken: userToken,
    userPartyId: partyId,
    orgToken: orgToken,
    org: org,
    app: app,
  };

  return data;
}

/*
 * 01 - GET app events for org. Query parameter 'after'
 * 02 - GET app  events for org based on 'next' url
 * 03 -  GET app events for party. Query parameters: partyId, after
 * 04 - GET app events for party based on 'next'
 */
export default function (data) {
  var response, success;

  // 01 - GET events for org. Query parameter 'after'
  response = appEventsApi.getEventsForOrg(
    data.org,
    data.app,
    { after: 1 },
    data.orgToken
  );

  var nextUrl = response.headers["Next"];

  success = check(response, {
    "01 - GET app events for org. Query parameter 'after'. Status is 200": (r) =>
      r.status === 200,
    "01 - GET app events for org. Query parameter 'after'. List contains minimum one element":
      (r) => JSON.parse(r.body).length > 1,
    "01 - GET app events for org. Query parameter 'after'. Continuation token provided":
      nextUrl,
  });

  addErrorCount(success);

  // 02 - GET events for org based on 'next' url
  response = appEventsApi.getEventsFromNextLink(nextUrl, data.orgToken);

  success = check(response, {
    "02 - GET app events for org based on 'next' url. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);

  // 03 -  GET app events for party. Query parameters: partyId, after.
  response = appEventsApi.getEventsForParty(
    { after: 1, party: data.userPartyId },
    data.userToken
  );

  var nextUrl = response.headers["Next"];

  success = check(response, {
    "03 - GET app events for party. Query parameters: partyId, after. Status is 200":
      (r) => r.status === 200,
    "03 - GET app events for party. Query parameters: partyId, after. List contains minimum one element":
      (r) => JSON.parse(r.body).length > 1,
    "03 - GET app events for party. Query parameters: partyId, after. Continuation token provided":
      nextUrl,
  });

  addErrorCount(success);

  // 04 - GET events for party based on 'next' url
  response = appEventsApi.getEventsFromNextLink(nextUrl, data.userToken);

  success = check(response, {
    "04 - GET app events for party based on 'next' url. Status is 200": (r) =>
      r.status === 200,
  });
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "platform-events");
  return result;
}
*/
