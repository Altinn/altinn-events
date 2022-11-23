/*
    Test script to platform events api with user token
    Command: docker-compose run k6 run /src/tests/events.js -e tokenGeneratorUserName=autotest -e tokenGeneratorUserPwd=*** -e env=***
*/
import { check } from "k6";
import * as setupToken from "../setup.js";
import * as eventsApi from "../api/events.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
const eventJson = JSON.parse(open("../data/event.json"));
import { generateJUnitXML, reportPath } from "../report.js";

const scopes = "altinn:events.publish";

export function setup() {
  var token = setupToken.getAltinnTokenForTTD(scopes);

  var cloudEvent = eventJson;
  cloudEvent.id = uuidv4();

  var data = {};
  data.token = token;
  data.cloudEvent = cloudEvent;
  return data;
}

export default function (data) {
  var resStatusCode, success;
  resStatusCode = eventsApi.postCloudEvent(
    JSON.stringify(data.cloudEvent),
    data.token
  );

  success = check(resStatusCode, {
    "POST valid cloud event status is 200": (r) => r === 200,
  });
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "platform-events");

  return result;
}
*/
