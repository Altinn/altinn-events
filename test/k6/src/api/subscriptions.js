import http from "k6/http";
import * as apiHelpers from "../apiHelpers.js";
import * as config from "../config.js";

export function getAllSubscriptions(token) {
  let endpoint = config.platformEvents.subscriptions;
  return getSubscriptions(endpoint, token);
}

export function getSubscriptionById(id, token) {
  let endpoint = config.platformEvents.subscriptions + "/" + id;
  return getSubscriptions(endpoint, token);
}

export function postSubscription(serializedSubscription, token) {
  let endpoint = config.platformEvents.subscriptions;

  let params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/json"
  );

  let response = http.post(endpoint, serializedSubscription, params);

  return response;
}

export function deleteSubscription(id, token) {
  let endpoint = config.platformEvents.subscriptions + id;
  let params = apiHelpers.buildHeaderWithBearer(token);
  let response = http.del(endpoint,null, params);
  return response;
}

function getSubscriptions(endpoint, token) {
  let params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/json"
  );

  let response = http.get(endpoint, params);

  return response;
}
