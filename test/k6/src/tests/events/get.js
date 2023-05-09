/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/events/get.js `
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
const eventJson = JSON.parse(open("../../data/events/01-event.json"));
import { generateJUnitXML, reportPath } from "../../report.js";
import { addErrorCount, stopIterationOnFail } from "../../errorhandler.js";
const scopes = "altinn:events.subscribe";

export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

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

// 01 - GET the first 10 cloud events published
function TC01_GetAllEvents(data) {
  var response, success;

  // we assume that /events/post.js has run at least once, publishing several events

  response = eventsApi.getCloudEvents(
    {
      after: 0,
      source: data.cloudEvent.source,
      type: data.cloudEvent.type,
      subject: data.cloudEvent.subject,
      size: 10,
    },
    data.token
  );

  success = check(response, {
    "GET all cloud events: status is 200": (r) => r.status === 200,
  });
  addErrorCount(success);

  if (!success) {
    // only continue to parse and check content if success response code
    stopIterationOnFail(success);
  }

  success = check(response, {
    "GET all cloud events: at least 1 cloud event returned": (r) => {
      var responseBody = JSON.parse(r.body);
      return Array.isArray(responseBody) && responseBody.length >= 1;
    },
  });

  addErrorCount(success);
}

// 02 - GET events and follow next link
function TC02_GetEventsAndFollowNextLink(data) {
  var response, success;

  response = eventsApi.getCloudEvents(
    {
      after: 0,
      source: data.cloudEvent.source,
      type: data.cloudEvent.type,
      size: 1,
    },
    data.token
  );

  var nextUrl = response.headers["Next"];

  success = check(response, {
    "GET cloud events: status is 200": (r) => r.status === 200,
    "GET cloud events: next link is provided ": (r) => nextUrl,
  });
  addErrorCount(success);

  response = eventsApi.getEventsFromNextLink(nextUrl, data.token);
  success = check(response, {
    "GET cloud events from next link: status is 200": (r) => r.status === 200,
  });

  addErrorCount(success);
}

/*
 * 01 - GET all existing cloud events for subject /party/1337
 * 02 - GET events and follow next link
 */
export default function (data) {
  try {
    if (data.runFullTestSet) {
      TC01_GetAllEvents(data);
      TC02_GetEventsAndFollowNextLink(data);
    } else {
      // Limited test set for use case tests
      TC01_GetAllEvents(data);
    }
  } catch (error) {
    addErrorCount(false);
    throw error;
  }
}
