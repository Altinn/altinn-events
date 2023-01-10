/*
    Test script to platform subscriptions api with user token
    Command: docker-compose run k6 run /src/tests/subscriptions.js -e tokenGeneratorUserName=autotest -e tokenGeneratorUserPwd=*** -e app=apps-test
*/
import { check } from "k6";
import * as setupToken from "../setup.js";
import * as subscriptionsApi from "../api/subscriptions.js";
import { generateJUnitXML, reportPath } from "../report.js";
import { addErrorCount } from "../errorhandler.js";
import * as config from "../config.js";

const scopes = "altinn:events.publish,altinn:events.subscribe";
const app = __ENV.app.toLowerCase();
const org = "ttd";

export function setup() {
  var orgToken = setupToken.getAltinnTokenForOrg(scopes);

  var data = {
    orgToken: orgToken,
    webhookToken: "ab242450-e4ee-4015-9ff1-517d24d64645", // todo: figure out how to greate guid
    org: org,
    app: app,
  };

  return data;
}

/*
 * 01 - POST new subscription for app event source
 * 02 - GET existing subscriptions for org
 * 03 - POST existing subscription
 * 04 -  GET existing subscriptions for org. Known count.
 * 05 - GET subscriptions by id
 * 06 - DELETE subscription
 */
export default function (data) {
  var response, success;
  let webhookEndpoint = `https://webhook.site/${data.webhookToken}`;

  // 01 - POST new subscription for app event source
  //    - use public webhook testing api for subscription verification
  var newSubscription = {
    endPoint: webhookEndpoint,
    sourceFilter: `https://${org}.apps.${config.baseUrl}/${org}/${app}`,
  };

  response = subscriptionsApi.postSubscription(
    JSON.stringify(newSubscription),
    data.orgToken
  );

  var subscription = JSON.parse(response.body);
  success = check(response, {
    "01 - POST new subscription for app event source. Status is 201": (r) =>
      r.status === 201,
    "01 - POST new subscription for app event source. Subscription id is defined":
      subscription.id != "undefined",
  });

  addErrorCount(success);
  // 02 - GET existing subscriptions for org
  response = subscriptionsApi.getAllSubscriptions(data.orgToken);

  var responseObject = JSON.parse(response.body);
  var subscriptionList = responseObject.subscriptions;

  success = check(response, {
    "02 - GET existing subscriptions for org. Status is 200.": (r) =>
      r.status === 200,
    "02 - GET existing subscriptions for org. Count is at least 1":
      responseObject.count >= 1,
    "02 - GET existing subscriptions for org. Auto test subscription in list":
      subscriptionList.some((s) => s.endPoint === webhookEndpoint),
  });

  addErrorCount(success);

  let expectedSubscriptionCount = responseObject.count;

  // 03 - POST existing subscription
  response = subscriptionsApi.postSubscription(
    JSON.stringify(newSubscription),
    data.orgToken
  );

  var subscription = JSON.parse(response.body);
  success = check(response, {
    "03 - POST existing subscription. Status is 201": (r) => r.status === 201,
    "03 - POST existing subscription. Subscription id is defined":
      subscription.id != "undefined",
  });

  addErrorCount(success);

  // 04 - GET existing subscriptions for org. Known count.
  response = subscriptionsApi.getAllSubscriptions(data.orgToken);

  var responseObject = JSON.parse(response.body);
  var subscriptionList = responseObject.subscriptions;

  success = check(response, {
    "04 - GET existing subscriptions for org again. Count matches expected subscription count":
      responseObject.count === expectedSubscriptionCount,
  });

  addErrorCount(success);

  // 05 - GET subscriptions by id
  response = subscriptionsApi.getSubscriptionById(
    subscription.id,
    data.orgToken
  );

  success = check(response, {
    "05 - GET subscriptions by id. Status is 200.": (r) => r.status === 200,
  });

  addErrorCount(success);
  // 06 - DELETE subscription
  response = subscriptionsApi.deleteSubscription(
    subscription.id,
    data.orgToken
  );

  success = check(response, {
    "06 - DELETE subscription. Status is 200.": (r) => r.status === 200,
  });

  addErrorCount(success);
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "events");

  return result;
}
*/
