/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/events/post.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e env=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/
import { check, sleep } from "k6";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
const eventJson = JSON.parse(open("../../data/events/01-event.json"));
import { generateJUnitXML, reportPath } from "../../report.js";
import { addErrorCount } from "../../errorhandler.js";
const scopes = "altinn:events.subscribe";

export const options = {
  thresholds: {
    errors: ['count<1'],
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

  response = eventsApi.getCloudEvents("0",
    data.cloudEvent.source,
    data.cloudEvent.type,
    data.cloudEvent.subject,
    10,
    data.token
  );


  success = check(response, {
    "GET all cloud events: status is 200": (r) => r.status === 200,
    "GET all cloud events: at least 1 cloud event returned": (r) =>
      Array.isArray(r.data) && r.data.length >= 1
  });

  addErrorCount(success);
}

/*
 * 01 - GET all existing cloud events for subject /party/1337
 */
export default function (data) {
  if (data.runFullTestSet) {
    TC01_GetAllEvents(data);

  } else {
    // Limited test set for use case tests
    TC01_GetAllEvents(data);
  }
}
