/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/events.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e env=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/
import { check } from "k6";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
const eventJson = JSON.parse(open("../data/events/01-event.json"));
import { generateJUnitXML, reportPath } from "../../report.js";
import { addErrorCount } from "../../errorhandler.js";
const scopes = "altinn:events.publish";

export function setup() {
  var token = setupToken.getAltinnTokenForOrg(scopes);

  var cloudEvent = eventJson;
  cloudEvent.id = uuidv4();

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  var data = {
    runFullTestSet: runFullTestSet,
    token: token,
    cloudEvent: cloudEvent,
  };

  return data;
}

// 01 - POST valid cloud event with all parameters
function TC01_POstValidCloudEventWithAllParameters(data) {
  var response, success;

  response = eventsApi.postCloudEvent(
    JSON.stringify(data.cloudEvent),
    data.token
  );

  success = check(response, {
    "POST valid cloud event with all parameters. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 02 - POST valid cloud event without subject
function TC02_PostValidCloudEventWithoutSubject(data) {
  var response, success;
  var cloudEventWithoutSubject = removePropFromCloudEvent(
    data.cloudEvent,
    "subject"
  );

  response = eventsApi.postCloudEvent(
    JSON.stringify(cloudEventWithoutSubject),
    data.token
  );

  success = check(response, {
    "POST valid cloud event without subject. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 03 - POST valid cloud event without time
function TC03_PostValidCloudEventWithoutTIme(data) {
  var response, success;

  var cloudEventWithoutTime = removePropFromCloudEvent(data.cloudEvent, "time");

  response = eventsApi.postCloudEvent(
    JSON.stringify(cloudEventWithoutTime),
    data.token
  );

  success = check(response, {
    "POST valid cloud event without time. Status is 200": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

// 04 - POST cloud event without bearer token
function TC04_PostCloudEventWithoutBearerToken(data) {
  var response, success;

  response = eventsApi.postCloudEvent(JSON.stringify(data.cloudEvent), "");

  success = check(response, {
    "POST cloud event without bearer token. Status is 401": (r) =>
      r.status === 401,
  });

  addErrorCount(success);
}

// 05 - POST cloud event without required scope
function TC05_PostCloudEventWithoutRequiredScopes(data) {
  var response, success;

  var scopeLessToken = setupToken.getAltinnTokenForOrg();

  response = eventsApi.postCloudEvent(
    JSON.stringify(data.cloudEvent),
    scopeLessToken
  );

  success = check(response, {
    "POST cloud event without required scope. Status is 403": (r) =>
      r.status === 403,
  });

  addErrorCount(success);
}

/*
 * 01 - POST valid cloud event with all parameters
 * 02 - POST valid cloud event without subject
 * 03 - POST valid cloud event without time
 * 04 - POST cloud event without bearer token
 * 05 - POST cloud event without required scope
 */
export default function (data) {
  if (data.runFullTestSet) {
    TC01_POstValidCloudEventWithAllParameters(data);

    TC02_PostValidCloudEventWithoutSubject(data);

    TC03_PostValidCloudEventWithoutTIme(data);

    TC04_PostCloudEventWithoutBearerToken(data);

    TC05_PostCloudEventWithoutRequiredScopes(data);
  } else {
    // Limited test set for use case tests

    TC01_POstValidCloudEventWithAllParameters(data);
  }
}

function removePropFromCloudEvent(cloudEvent, propertyname) {
  // parse and stringify to ensure a copy of the object by value not ref
  var modifiedEvent = JSON.parse(JSON.stringify(cloudEvent));
  delete modifiedEvent[propertyname];

  return modifiedEvent;
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "events");

  return result;
}
*/
