/*
    Test script to platform subscriptions api with user token
    Command:
      docker-compose run k6 run /src/tests/subscriptions.js `
      -e env=*** `
      -e tokenGeneratorUserName=autotest `
      -e tokenGeneratorUserPwd=*** `
      -e mpClientId=*** `
      -e mpKid=altinn-usecase-events `
      -e encodedJwk=*** `
      -e app=apps-test `
      -e webhookEndpoint=***** `
      -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
    */

import { check, sleep } from "k6";
import * as subscriptionsApi from "../api/subscriptions.js";
import * as config from "../config.js";
import { addErrorCount } from "../errorhandler.js";
import * as setupToken from "../setup.js";

const appSubscription = JSON.parse(
  open("../data/subscriptions/01-app-subscription.json")
);

const genericSubscription = JSON.parse(
  open("../data/subscriptions/02-generic-subscription.json")
);

const scopes =
  "altinn:serviceowner altinn:events.publish altinn:events.subscribe";

const webhookEndpoint = __ENV.webhookEndpoint;

const org = "ttd";
const app = (__ENV.app || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)
const runFullTestSet = __ENV.runFullTestSet
  ? __ENV.runFullTestSet.toLowerCase().includes("true")
  : false;

appSubscription.sourceFilter = `https://${org}.apps.${config.baseUrl}/${org}/${app}`;
appSubscription.endPoint = webhookEndpoint;
genericSubscription.endPoint = webhookEndpoint;

export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

export function setup() {
  let orgToken = setupToken.getAltinnTokenForOrg(scopes);
  let incorrectScopeToken = setupToken.getAltinnTokenForOrg(
    "altinn:serviceowner"
  );

  let data = {
    runFullTestSet: runFullTestSet,
    orgToken: orgToken,
    incorrectScopeToken: incorrectScopeToken,
    org: org,
    app: app,
    appSubscription: JSON.stringify(appSubscription),
    genericSubscription: JSON.stringify(genericSubscription),
    webhookEndpoint: webhookEndpoint,
  };

  return data;
}

// 01 - POST new subscription for app event source
function TC01_PostNewSubscriptionForAppEventSource(data) {
  let response, success;

  response = subscriptionsApi.postSubscription(
    data.appSubscription,
    data.orgToken
  );

  let subscription = JSON.parse(response.body);
  success = check(response, {
    "01 - POST new subscription for app event source. Status is 201": (r) =>
      r.status === 201,
    "01 - POST new subscription for app event source. Subscription id is defined":
      subscription.id != "undefined",
  });

  addErrorCount(success);
  return subscription.id;
}

// 02 - GET existing subscriptions for org
function TC02_GetExistingSubscriptionsForOrg(data) {
  let response = subscriptionsApi.getAllSubscriptions(data.orgToken);

  let subscriptionList = JSON.parse(response.body);
  let subscriptions = subscriptionList.subscriptions;

  let success = check(response, {
    "02 - GET existing subscriptions for org. Status is 200.": (r) =>
      r.status === 200,
    "02 - GET existing subscriptions for org. Count is at least 1":
      subscriptionList.count >= 1,
    "02 - GET existing subscriptions for org. Auto test subscription in list":
      subscriptions.some((s) => s.endPoint === data.webhookEndpoint),
  });

  addErrorCount(success);

  return subscriptionList.count;
}

// 03 - POST existing subscription
function TC03_PostExistingSubscription(data) {

  let response = subscriptionsApi.postSubscription(
    data.appSubscription,
    data.orgToken
  );

  let subscription = JSON.parse(response.body);
  let success = check(response, {
    "03 - POST existing subscription. Status is 201": (r) => r.status === 201,
    "03 - POST existing subscription. Subscription id is defined":
      subscription.id != "undefined",
  });

  addErrorCount(success);
}

// 04 - GET existing subscriptions for org. Known count.

function TC04_GetExistingSubscriptionsForOrg(data, expectedSubscriptionCount) {
  let response = subscriptionsApi.getAllSubscriptions(data.orgToken);

  let responseObject = JSON.parse(response.body);
  let success = check(response, {
    "04 - GET existing subscriptions for org again. Count matches expected subscription count":
      responseObject.count === expectedSubscriptionCount,
  });

  addErrorCount(success);
}

// 05 - GET subscription by id
function TC05_GetSubscriptionById(data, subscriptionId) {
  // delay to ensure subscription has time to be validated
  sleep(15);

  let response = subscriptionsApi.getSubscriptionById(
    subscriptionId,
    data.orgToken
  );

  let success = check(response, {
    "05 - GET subscriptions by id. Status is 200.": (r) => r.status === 200,
  });

  addErrorCount(success);

  if (success) {
    let subscription = JSON.parse(response.body);

    success = check(response, {
      "05 - Get subscription by id. Returned subscription is validated":
        subscription.validated,
    });
    addErrorCount(success);
  }
}

// 06 - DELETE subscription
function TC06_DeleteSubscription(data, subscriptionId) {
  let response = subscriptionsApi.deleteSubscription(subscriptionId, data.orgToken);
  let success = check(response, {
    "06 - DELETE subscription. Status is 200.": (r) => r.status === 200,
  });

  addErrorCount(success);
}

//  07 - POST subscription for external event source
function TC07_PostSubscriptionExternalEventSource(data) {
  let response = subscriptionsApi.postSubscription(
    data.genericSubscription,
    data.orgToken
  );

  let subscription = JSON.parse(response.body);
  let success = check(response, {
    "07 - POST subscription for external event source. Status is 201": (r) =>
      r.status === 201,
    "07 - POST subscription for external event source. Subscription id is defined":
      subscription.id != "undefined",
  });

  addErrorCount(success);

  return subscription.id;
}

// 08 - POST subscription for external event source without required scope
function TC08_PostSubscriptionForExternalEventSourceWithoutScope(data) {
  let response = subscriptionsApi.postSubscription(
    data.genericSubscription,
    data.incorrectScopeToken
  );

  let success = check(response, {
    "08 - POST subscription for external event source without required scope. Status is 403":
      (r) => r.status === 403,
  });

  addErrorCount(success);
}
/*
 * 01 - POST new subscription for app event source
 * 02 - GET existing subscriptions for org
 * 03 - POST existing subscription for app event source
 * 04 - GET existing subscriptions for org. No change expected.
 * 05 - GET subscription by id
 * 06 - DELETE subscription
 * 07 - POST subscription for external event source
 * 08 - POST subscription for external event source without required scope
 */
export default function runTests(data) {
  try {
    if (data.runFullTestSet) {
      const appSubscriptionId = TC01_PostNewSubscriptionForAppEventSource(data);

      const currentSubscriptionCount =
        TC02_GetExistingSubscriptionsForOrg(data);

      TC03_PostExistingSubscription(data);

      TC04_GetExistingSubscriptionsForOrg(data, currentSubscriptionCount);

      TC05_GetSubscriptionById(data, appSubscriptionId);

      TC06_DeleteSubscription(data, appSubscriptionId);

      const genericSubscriptionId =
        TC07_PostSubscriptionExternalEventSource(data);

      if (genericSubscriptionId) {
        TC06_DeleteSubscription(data, genericSubscriptionId);
      }

      TC08_PostSubscriptionForExternalEventSourceWithoutScope(data);
    } else {
      // Limited test set for use case tests
      const appSubscriptionId = TC01_PostNewSubscriptionForAppEventSource(data);

      TC02_GetExistingSubscriptionsForOrg(data);

      TC05_GetSubscriptionById(data, appSubscriptionId);

      TC06_DeleteSubscription(data, appSubscriptionId);
    }
  } catch (error) {
    addErrorCount(false);
    throw error;
  }
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "events");

  return result;
}
*/
