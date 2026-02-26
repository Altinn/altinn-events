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
      -e runFullTestSet=true `
      -e useCSVData=false

    For single line version of the above command, use the following command:
    docker-compose run k6 run /src/tests/subscriptions.js -e tokenGeneratorUserName=autotest -e tokenGeneratorUserPwd=*** -e altinn_env=at22 -e runFullTestSet=true -e useCSVData=true -e webhookEndpoint=*** --vus 1 --duration 30s -e app=apps-test

    For use case tests omit environment variable runFullTestSet or set value to false
    Set useCSVData=true to load test data from CSV file instead of JSON
    Update the variables --vus and --duration as needed for performance testing
*/

import { check, sleep } from "k6";
import * as subscriptionsApi from "../api/subscriptions.js";
import * as config from "../config.js";
import { addErrorCount } from "../errorhandler.js";
import * as setupToken from "../setup.js";
import { loadCSV, loadJSONDirectory, loadJSON, createSubscriptionFromCSV, getItemByVU } from "../dataLoader.js";

const useCSVData = (__ENV.useCSVData || "").toLowerCase() === "true";

const subscriptionVariations = useCSVData 
              ? loadCSV('subscription-variations', '../data/subscriptions/subscription-variations.csv') : 
              loadJSONDirectory('subscription-variations', '../data/subscriptions/', 
              [
                '01-app-subscription.json',
                '02-generic-subscription.json'
              ]);

console.log(`[SUBSCRIPTIONS] Using CSV data: ${useCSVData}`);
console.log(`[SUBSCRIPTIONS] Subscription variations loaded: ${subscriptionVariations.length}`);

const scopes =
  "altinn:serviceowner altinn:events.publish altinn:events.subscribe";

const webhookEndpoint = __ENV.webhookEndpoint;

const org = "ttd";
const app = (__ENV.app || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)
const runFullTestSet = __ENV.runFullTestSet
  ? __ENV.runFullTestSet.toLowerCase().includes("true")
  : false;

// Helper function to create subscription dynamically during test execution
function createSubscription(subscriptionType = 'app') {
    let subscriptionData;
    
    if (useCSVData) {
        // SharedArray doesn't support filter - manually iterate
        const filteredRows = [];
        for (let i = 0; i < subscriptionVariations.length; i++) {
            if (subscriptionVariations[i].type === subscriptionType) {
                filteredRows.push(subscriptionVariations[i]);
            }
        }
        if (filteredRows.length === 0) {
            console.error(`[ERROR] No ${subscriptionType} subscriptions found in CSV data`);
            throw new Error(`No ${subscriptionType} subscriptions in CSV`);
        }
        subscriptionData = getItemByVU(filteredRows, __VU);
    } else {
        // For JSON: subscriptionVariations[0] = app, subscriptionVariations[1] = generic
        const templateIndex = subscriptionType === 'app' ? 0 : 1;
        subscriptionData = subscriptionVariations[templateIndex];
    }
    
    let subscription;
    if (useCSVData) {
        // Create subscription from CSV row
        // Make endpoint unique per VU and iteration to avoid conflicts in load tests
        subscription = createSubscriptionFromCSV(subscriptionData, {
            endPoint: `${webhookEndpoint}?vu=${__VU}&iter=${__ITER}`
        });
        
        // Handle filter mapping based on subscription type
        if (subscriptionType === 'app') {
            // Override sourceFilter for app subscriptions with actual app name
            subscription.sourceFilter = `https://${org}.apps.${config.baseUrl}/${org}/${app}`;
            delete subscription.resourceFilter;
            delete subscription.consumer;
        } else if (subscriptionType === 'generic') {
            // For generic subscriptions, use a valid URN for resourceFilter
            subscription.resourceFilter = "urn:altinn:resource:ttd-altinn-events-automated-tests";
            delete subscription.sourceFilter;
            
            // Add consumer if not present
            if (!subscription.consumer) {
                subscription.consumer = "norsknettavisleser";
            }
        }
    } else {
        // Use JSON subscription directly
        subscription = { ...subscriptionData };
        // Make endpoint unique per VU and iteration to avoid conflicts
        subscription.endPoint = `${webhookEndpoint}?vu=${__VU}&iter=${__ITER}`;
        
        // Set sourceFilter for app subscriptions
        if (subscriptionType === 'app') {
            subscription.sourceFilter = `https://${org}.apps.${config.baseUrl}/${org}/${app}`;
            // Remove resourceFilter if present (app subscriptions use sourceFilter)
            delete subscription.resourceFilter;
            delete subscription.consumer;
        }
    }
    
    console.log(`[SUBSCRIPTIONS VU${__VU}] Creating ${subscriptionType} subscription - endpoint: ${subscription.endPoint}`);
    
    return subscription;
}

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
    webhookEndpoint: webhookEndpoint,
    useCSVData: useCSVData,
  };

  return data;
}

// 01 - POST new subscription for app event source
function TC01_PostNewSubscriptionForAppEventSource(data) {
  let response, success;

  let appSubscription = createSubscription('app');

  response = subscriptionsApi.postSubscription(
    JSON.stringify(appSubscription),
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
  return { id: subscription.id, endpoint: appSubscription.endPoint };
}

// 02 - GET existing subscriptions for org
function TC02_GetExistingSubscriptionsForOrg(data, expectedEndpoint) {
  let response = subscriptionsApi.getAllSubscriptions(data.orgToken);

  let subscriptionList = JSON.parse(response.body);
  let subscriptions = subscriptionList.subscriptions;

  // Use the expected endpoint (with VU/iter params if provided) or base webhook endpoint
  const endpointToCheck = expectedEndpoint || data.webhookEndpoint;
  
  let success = check(response, {
    "02 - GET existing subscriptions for org. Status is 200.": (r) =>
      r.status === 200,
    "02 - GET existing subscriptions for org. Count is at least 1":
      subscriptionList.count >= 1,
    "02 - GET existing subscriptions for org. Auto test subscription in list":
      subscriptions.some((s) => s.endPoint === endpointToCheck),
  });

  addErrorCount(success);

  return subscriptionList.count;
}

// 03 - POST existing subscription
function TC03_PostExistingSubscription(data) {
  let appSubscription = createSubscription('app');

  let response = subscriptionsApi.postSubscription(
    JSON.stringify(appSubscription),
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
  let genericSubscription = createSubscription('generic');

  let response = subscriptionsApi.postSubscription(
    JSON.stringify(genericSubscription),
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
  let genericSubscription = createSubscription('generic');

  let response = subscriptionsApi.postSubscription(
    JSON.stringify(genericSubscription),
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
      const appSubscriptionData = TC01_PostNewSubscriptionForAppEventSource(data);

      const currentSubscriptionCount =
        TC02_GetExistingSubscriptionsForOrg(data, appSubscriptionData.endpoint);

      TC03_PostExistingSubscription(data);

      TC04_GetExistingSubscriptionsForOrg(data, currentSubscriptionCount);

      TC05_GetSubscriptionById(data, appSubscriptionData.id);

      TC06_DeleteSubscription(data, appSubscriptionData.id);

      const genericSubscriptionId =
        TC07_PostSubscriptionExternalEventSource(data);

      if (genericSubscriptionId) {
        TC06_DeleteSubscription(data, genericSubscriptionId);
      }

      TC08_PostSubscriptionForExternalEventSourceWithoutScope(data);
    } else {
      // Limited test set for use case tests
      const appSubscriptionData = TC01_PostNewSubscriptionForAppEventSource(data);

      TC02_GetExistingSubscriptionsForOrg(data, appSubscriptionData.endpoint);

      TC05_GetSubscriptionById(data, appSubscriptionData.id);

      TC06_DeleteSubscription(data, appSubscriptionData.id);
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
