/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/events/post.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e mpClientId=*** `
    -e mpKid=altinn-usecase-events `
    -e encodedJwk=*** `
    -e env=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/
import { check } from "k6";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
const eventJson = JSON.parse(open("../../data/events/01-event.json"));
import { addErrorCount } from "../../errorhandler.js";
const scopes = "altinn:events.publish altinn:serviceowner";

export const options = {
    thresholds: {
        error_rate: ["count<1"],
    },
};

export function setup() {
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    const data = {
        runFullTestSet: runFullTestSet,
        token: token,
    };

    return data;
}

function createCloudEvent() {
    const cloudEvent = { ...eventJson };
    cloudEvent.id = uuidv4();
    return cloudEvent;
}

// 01 - POST valid cloud event with all parameters
function TC01_POstValidCloudEventWithAllParameters(data) {
    let response, success, cloudEvent;

    cloudEvent = createCloudEvent();

    response = eventsApi.postCloudEvent(
        JSON.stringify(cloudEvent),
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
    let response, success, cloudEvent;

    cloudEvent = createCloudEvent();

    let cloudEventWithoutSubject = removePropFromCloudEvent(
        cloudEvent,
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
function TC03_PostValidCloudEventWithoutTime(data) {
    let response, success, cloudEvent;

    cloudEvent = createCloudEvent();

    let cloudEventWithoutTime = removePropFromCloudEvent(cloudEvent, "time");

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
function TC04_PostCloudEventWithoutBearerToken() {
    let response, success, cloudEvent;

    cloudEvent = createCloudEvent();

    response = eventsApi.postCloudEvent(JSON.stringify(cloudEvent), "");

    success = check(response, {
        "POST cloud event without bearer token. Status is 401": (r) =>
            r.status === 401,
    });

    addErrorCount(success);
}

// 05 - POST cloud event without required scope
function TC05_PostCloudEventWithoutRequiredScopes() {
    let response, success, cloudEvent;

    cloudEvent = createCloudEvent();

    let incorrectScopeToken = setupToken.getAltinnTokenForOrg('altinn:serviceowner');

    response = eventsApi.postCloudEvent(
        JSON.stringify(cloudEvent),
        incorrectScopeToken
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
export default function runTests(data) {
    try {
        if (data.runFullTestSet) {
            TC01_POstValidCloudEventWithAllParameters(data);

            TC02_PostValidCloudEventWithoutSubject(data);

            TC03_PostValidCloudEventWithoutTime(data);

            TC04_PostCloudEventWithoutBearerToken();

            TC05_PostCloudEventWithoutRequiredScopes();
        } else {
            // Limited test set for use case tests

            TC01_POstValidCloudEventWithAllParameters(data);
        }
    } catch (error) {
        addErrorCount(false);
        throw error;
    }
}

function removePropFromCloudEvent(cloudEvent, propertyname) {
    // parse and stringify to ensure a copy of the object by value not ref
    let modifiedEvent = JSON.parse(JSON.stringify(cloudEvent));
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
